using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Server;

namespace OpcUaXE.UnitTests.Infrastructure;

/// <summary>
/// OPC UA node manager that creates a deterministic set of test nodes under namespace
/// "http://opcuaxe.test/" (dynamically assigned as ns=2).
/// </summary>
internal sealed class TestNodeManager : CustomNodeManager2
{
    private const string TestNamespace = "http://opcuaxe.test/";

    /// <summary>Telemetry for <see cref="AlarmConditionState"/> (not available on <see cref="IServerInternal"/> during <see cref="CreateAddressSpace"/>).</summary>
    private static readonly ITelemetryContext AlarmTelemetry = DefaultTelemetry.Create(static _ => { });

    /// <summary>Condition name exposed on the integration-test alarm (client <c>ConditionName</c> field).</summary>
    internal const string IntegrationAlarmConditionName = "OpcUaXeIntegrationAlarm";

    private AlarmConditionState? _integrationAlarm;
    private BaseObjectState? _alarmEventSource;

    public TestNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration, TestNamespace)
    {
    }

    protected override NodeStateCollection LoadPredefinedNodes(ISystemContext context)
        => new NodeStateCollection();

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            FolderState testFolder = CreateFolder(null, "TestNodes", "TestNodes");

            // Link the folder into the standard Objects folder (i=85).
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out IList<IReference>? refs))
            {
                refs = new List<IReference>();
                externalReferences[ObjectIds.ObjectsFolder] = refs;
            }
            refs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, testFolder.NodeId));
            testFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);

            CreateVariable(testFolder, "BoolNode", DataTypeIds.Boolean, false);
            CreateVariable(testFolder, "Int32Node", DataTypeIds.Int32, (int)42);
            CreateVariable(testFolder, "DoubleNode", DataTypeIds.Double, 3.14d);
            CreateVariable(testFolder, "StringNode", DataTypeIds.String, "hello");
            CreateVariable(testFolder, "MonitorNode", DataTypeIds.Int32, (int)0);

            _alarmEventSource = new BaseObjectState(testFolder)
            {
                SymbolicName = "AlarmEventSource",
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = ObjectTypeIds.BaseObjectType,
                NodeId = new NodeId("AlarmEventSource", NamespaceIndex),
                BrowseName = new QualifiedName("AlarmEventSource", NamespaceIndex),
                DisplayName = new LocalizedText("en", "AlarmEventSource"),
                WriteMask = AttributeWriteMask.None,
                UserWriteMask = AttributeWriteMask.None,
                EventNotifier = EventNotifiers.SubscribeToEvents
            };
            testFolder.AddChild(_alarmEventSource);

            AddPredefinedNode(SystemContext, testFolder);

            var sctx = (ServerSystemContext)SystemContext;
            _integrationAlarm = new AlarmConditionState(AlarmTelemetry, _alarmEventSource);
            CreateNode(
                sctx,
                _alarmEventSource.NodeId,
                ReferenceTypeIds.HasCondition,
                new QualifiedName("IntegrationTestAlarm", NamespaceIndex),
                _integrationAlarm);

            AddBehaviourToPredefinedNode(SystemContext, _integrationAlarm);
            AddRootNotifier(_alarmEventSource);

            _integrationAlarm.ConditionName.Value = IntegrationAlarmConditionName;
            _integrationAlarm.Message.Value = new LocalizedText("en", "integration alarm active");
            _integrationAlarm.SetSeverity(SystemContext, (EventSeverity)850);
            _integrationAlarm.Severity.Value = 850;
            _integrationAlarm.SetEnableState(SystemContext, true);
            _integrationAlarm.SetActiveState(SystemContext, true);
            _integrationAlarm.SetAcknowledgedState(SystemContext, true);
            _integrationAlarm.ReportEvent(SystemContext, _alarmEventSource!);
        }
    }

    internal void RaiseIntegrationTestAlarm()
    {
        lock (Lock)
        {
            if (_integrationAlarm == null || _alarmEventSource == null)
                return;

            _integrationAlarm.SetActiveState(SystemContext, false);
            _integrationAlarm.SetAcknowledgedState(SystemContext, true);
            _integrationAlarm.ReportEvent(SystemContext, _alarmEventSource);

            _integrationAlarm.Message.Value = new LocalizedText("en", "integration alarm active");
            _integrationAlarm.SetSeverity(SystemContext, (EventSeverity)850);
            _integrationAlarm.Severity.Value = 850;
            _integrationAlarm.SetActiveState(SystemContext, true);
            _integrationAlarm.SetAcknowledgedState(SystemContext, true);
            _integrationAlarm.ReportEvent(SystemContext, _alarmEventSource);
        }
    }

    internal void ClearIntegrationTestAlarm()
    {
        lock (Lock)
        {
            if (_integrationAlarm == null || _alarmEventSource == null)
                return;

            _integrationAlarm.SetAcknowledgedState(SystemContext, true);
            _integrationAlarm.Message.Value = new LocalizedText("en", "integration alarm cleared");
            _integrationAlarm.SetSeverity(SystemContext, (EventSeverity)50);
            _integrationAlarm.Severity.Value = 50;
            _integrationAlarm.SetActiveState(SystemContext, false);
            _integrationAlarm.ReportEvent(SystemContext, _alarmEventSource);
        }
    }

    // NODE FACTORY HELPERS ##########

    private FolderState CreateFolder(NodeState? parent, string symbolicName, string displayName)
    {
        FolderState folder = new(parent)
        {
            SymbolicName = symbolicName,
            ReferenceTypeId = ReferenceTypeIds.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            NodeId = new NodeId(symbolicName, NamespaceIndex),
            BrowseName = new QualifiedName(symbolicName, NamespaceIndex),
            DisplayName = new LocalizedText("en", displayName),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None
        };
        parent?.AddChild(folder);
        return folder;
    }

    private BaseDataVariableState CreateVariable(
        NodeState parent, string symbolicName, NodeId dataType, object initialValue)
    {
        BaseDataVariableState variable = new(parent)
        {
            SymbolicName = symbolicName,
            ReferenceTypeId = ReferenceTypeIds.HasComponent,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId(symbolicName, NamespaceIndex),
            BrowseName = new QualifiedName(symbolicName, NamespaceIndex),
            DisplayName = new LocalizedText("en", symbolicName),
            WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            DataType = dataType,
            ValueRank = ValueRanks.Scalar,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Historizing = false,
            Value = initialValue,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };
        parent.AddChild(variable);
        return variable;
    }
}
