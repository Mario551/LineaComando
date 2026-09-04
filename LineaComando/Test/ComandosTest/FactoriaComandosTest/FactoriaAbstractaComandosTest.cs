using ComandosTest.FactoriaComandosTest.FactoriaComandosTest;
using PER.Comandos.LineaComandos;
using PER.Comandos.LineaComandos.Comando;
using PER.Comandos.LineaComandos.Excepcion;
using PER.Comandos.LineaComandos.FactoriaComandos;

namespace ComandosTest.FactoriaComandosTest;

public class FactoriaAbstractaComandosTest
{
    [Fact]
    public void Crear_ConMismaRutaLocal_DebeUsarLaFactoriaIndicada()
    {
        FactoriaComandos<string, string> pedidos = new("pedido");
        pedidos.Add("consultar", new Nodo<string, string>(new ComandoPrueba1()));
        FactoriaComandos<string, string> clientes = new("cliente");
        clientes.Add("consultar", new Nodo<string, string>(new ComandoPrueba2()));
        FactoriaAbstractaComandos<string, string> factoria = new([pedidos, clientes]);

        LineaComando lineaPedido = new(["pedido", "consultar", "--id=1"]);
        LineaComando lineaCliente = new(["cliente", "consultar"]);

        IComando<string, string> comandoPedido = factoria.Crear(lineaPedido);
        IComando<string, string> comandoCliente = factoria.Crear(lineaCliente);

        Assert.IsType<ComandoPrueba1>(comandoPedido);
        Assert.IsType<ComandoPrueba2>(comandoCliente);
        Assert.Equal(["pedido", "consultar"], lineaPedido.Ruta);
        Assert.Single(lineaPedido.Parametros);
    }

    [Fact]
    public void Add_DebePermitirResolverLaFactoriaPorNombre()
    {
        FactoriaComandos<string, string> pedidos = new("pedido");
        FactoriaAbstractaComandos<string, string> factoria = new([]);

        factoria.Add(pedidos);

        Assert.Same(pedidos, factoria.Get("pedido"));
    }

    [Fact]
    public void Constructor_ConNombresDuplicados_DebeLanzarExcepcion()
    {
        FactoriaComandos<string, string> primera = new("pedido");
        FactoriaComandos<string, string> segunda = new("pedido");

        Assert.Throws<InvalidOperationException>(() =>
            new FactoriaAbstractaComandos<string, string>([primera, segunda]));
    }

    [Fact]
    public void Crear_ConFactoriaDesconocida_DebeLanzarExcepcion()
    {
        FactoriaAbstractaComandos<string, string> factoria = new(
            [new FactoriaComandos<string, string>("pedido")]);

        Assert.Throws<NoEncontradoExcepcion>(() =>
            factoria.Crear(new LineaComando(["cliente", "consultar"])));
    }

    [Fact]
    public void Crear_DebeDistinguirMayusculasYMinusculas()
    {
        FactoriaAbstractaComandos<string, string> factoria = new(
            [new FactoriaComandos<string, string>("pedido")]);

        Assert.Throws<NoEncontradoExcepcion>(() =>
            factoria.Crear(new LineaComando(["Pedido", "consultar"])));
    }

    [Fact]
    public void Crear_ConRutaSinComando_DebeLanzarExcepcion()
    {
        FactoriaAbstractaComandos<string, string> factoria = new(
            [new FactoriaComandos<string, string>("pedido")]);

        Assert.Throws<ErrorDeSintaxisExcepcion>(() =>
            factoria.Crear(new LineaComando(["pedido"])));
    }
}
