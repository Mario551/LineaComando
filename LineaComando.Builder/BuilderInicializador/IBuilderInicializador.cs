using PER.Comandos.LineaComandos.BuilderComando;
using PER.Comandos.LineaComandos.BuilderTipoEvento;

namespace PER.Comandos.LineaComandos.BuilderInicializador;

public interface IBuilderInicializador
{
    IBuilderComando NewBuilderComando();
    IBuilderTipoEvento NewBuilderTipoEvento();
}