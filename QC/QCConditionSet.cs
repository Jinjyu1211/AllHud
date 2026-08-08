using System.Text.Json.Serialization;

namespace AllHud;

public enum QCConditionType {
    None,
    InCombat,
    OutOfCombat,
    InInstance,
    OutOfInstance,
    ClassJobId,
    TerritoryId,
    Mounted,
    WeaponDrawn,
    Swimming,
    Crafting,
    Gathering,
    InGpose,
    BetweenAreas,
    DutyReady,
    DutyReadyConfirm,
    BoundByDuty56,
    BoundByDuty97,
    NoviceNetwork,
    WaitingForDuty,
    ConditionSet,
}

public enum QCBinaryOperator {
    AND,
    OR,
    EQUALS,
    XOR,
}

public sealed class QCConditionEntry {
    public QCConditionType ConditionType { get; set; } = QCConditionType.InCombat;
    public List<uint> TargetIds { get; set; } = [];
    public bool Negate { get; set; }
    public QCBinaryOperator Operator { get; set; } = QCBinaryOperator.AND;
}

public sealed class QCConditionSetDefinition {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "新条件集";
    public List<QCConditionEntry> Conditions { get; set; } = [new()];
}