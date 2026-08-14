using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PER.Comandos.LineaComandos.EventDriven.Bus
{
    internal abstract class ObservadorNotificacionEnMemoria<TNotificacion> : IDisposable
    {
        private enum ModoObservacion
        {
            SinDefinir = 0,
            Callback = 1,
            Espera = 2
        }

        private readonly object _sincronizacion = new object();
        private readonly string _clave;
        private readonly string _descripcion;
        private readonly Action<ObservadorNotificacionEnMemoria<TNotificacion>> _alDisponer;
        private readonly ILogger _logger;
        private readonly Channel<TNotificacion> _notificaciones;
        private readonly CancellationTokenSource _cancelacion = new CancellationTokenSource();

        private Func<TNotificacion, CancellationToken, Task>? _callbacks;
        private ModoObservacion _modo;
        private Task? _procesamientoCallbacks;
        private bool _dispuesto;
        private int _esperaActiva;

        protected ObservadorNotificacionEnMemoria(
            string clave,
            string descripcion,
            Action<ObservadorNotificacionEnMemoria<TNotificacion>> alDisponer,
            ILogger logger)
        {
            _clave = clave;
            _descripcion = descripcion;
            _alDisponer = alDisponer;
            _logger = logger;
            _notificaciones = Channel.CreateUnbounded<TNotificacion>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                });
        }

        protected void AgregarCallback(
            Func<TNotificacion, CancellationToken, Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            lock (_sincronizacion)
            {
                ValidarNoDispuesto();
                SeleccionarModoSiNoEstaDefinido(ModoObservacion.Callback);
                _callbacks += callback;
                _procesamientoCallbacks ??= ProcesarCallbacksAsync();
            }
        }

        protected void QuitarCallback(
            Func<TNotificacion, CancellationToken, Task> callback)
        {
            lock (_sincronizacion)
            {
                if (_dispuesto)
                    return;

                _callbacks -= callback;
            }
        }

        protected async Task<TNotificacion> EsperarInternoAsync(
            CancellationToken cancellationToken)
        {
            lock (_sincronizacion)
            {
                ValidarNoDispuesto();
                SeleccionarModoSiNoEstaDefinido(ModoObservacion.Espera);
            }

            if (Interlocked.CompareExchange(ref _esperaActiva, 1, 0) != 0)
                throw new InvalidOperationException("Solo se permite una espera activa por observador.");

            try
            {
                using CancellationTokenSource cancelacionVinculada =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        _cancelacion.Token);

                try
                {
                    return await LeerSiguienteAsync(cancelacionVinculada.Token);
                }
                catch (OperationCanceledException) when (_cancelacion.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                catch (ChannelClosedException) when (_dispuesto)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
            }
            finally
            {
                Volatile.Write(ref _esperaActiva, 0);
            }
        }

        internal void Notificar(TNotificacion notificacion)
        {
            _notificaciones.Writer.TryWrite(notificacion);
        }

        public void Dispose()
        {
            lock (_sincronizacion)
            {
                if (_dispuesto)
                    return;

                _dispuesto = true;
                _callbacks = null;
            }

            _alDisponer(this);
            _cancelacion.Cancel();
            _notificaciones.Writer.TryComplete();
        }

        private async Task ProcesarCallbacksAsync()
        {
            try
            {
                while (!_cancelacion.IsCancellationRequested)
                {
                    TNotificacion notificacion = await LeerSiguienteAsync(_cancelacion.Token);
                    Func<TNotificacion, CancellationToken, Task>[] callbacks;

                    lock (_sincronizacion)
                    {
                        callbacks = _callbacks?
                            .GetInvocationList()
                            .Cast<Func<TNotificacion, CancellationToken, Task>>()
                            .ToArray()
                            ?? Array.Empty<Func<TNotificacion, CancellationToken, Task>>();
                    }

                    foreach (Func<TNotificacion, CancellationToken, Task> callback in callbacks)
                    {
                        try
                        {
                            await callback(notificacion, _cancelacion.Token);
                        }
                        catch (OperationCanceledException) when (_cancelacion.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error ejecutando observador de {Descripcion} {Clave}.",
                                _descripcion,
                                _clave);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (_cancelacion.IsCancellationRequested)
            {
            }
            catch (ChannelClosedException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "El observador de {Descripcion} {Clave} finalizó inesperadamente.",
                    _descripcion,
                    _clave);
            }
        }

        private ValueTask<TNotificacion> LeerSiguienteAsync(
            CancellationToken cancellationToken)
        {
            return _notificaciones.Reader.ReadAsync(cancellationToken);
        }

        private void SeleccionarModoSiNoEstaDefinido(ModoObservacion modo)
        {
            if (_modo == ModoObservacion.SinDefinir)
            {
                _modo = modo;
                return;
            }

            if (_modo != modo)
            {
                throw new InvalidOperationException(
                    "Un observador no puede combinar callbacks con esperas mediante await.");
            }
        }

        private void ValidarNoDispuesto()
        {
            ObjectDisposedException.ThrowIf(_dispuesto, this);
        }
    }
}
