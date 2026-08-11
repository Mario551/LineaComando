using PER.Mensajeria.Entidad.Infobip.Interfaz;

namespace PER.Mensajeria.Entidad.Infobip.DAO;

public class FlowResponseNodeInfobip : IAuditableEntity
{
    public long RecordId { get; set; }
    public long RecordIdFlowReplyMessagesInfobip { get; set; }
    public long? RecordIdFlowResponseNodesInfobipParent { get; set; }
    public string? Key { get; set; }
    public int? ElementIndex { get; set; }
    public required string NodeType { get; set; }
    public string? TextValue { get; set; }
    public decimal? NumericValue { get; set; }
    public bool? BooleanValue { get; set; }
    public DateTime RecordCreatedAt { get; set; }
    public DateTime? RecordUpdatedAt { get; set; }

    public virtual FlowReplyMessageInfobip FlowReplyMessageInfobip { get; set; } = null!;
    public virtual FlowResponseNodeInfobip? Parent { get; set; }
    public virtual ICollection<FlowResponseNodeInfobip> Children { get; set; } = [];
}
