namespace PER.Mensajeria.Entidad.Infobip.DTO;

public class DTOInfobipFlowResponseNode
{
    public string? Key { get; set; }

    public int? ElementIndex { get; set; }

    public required string NodeType { get; set; }

    public string? TextValue { get; set; }

    public decimal? NumericValue { get; set; }

    public bool? BooleanValue { get; set; }

    public List<DTOInfobipFlowResponseNode> Children { get; set; } = [];
}
