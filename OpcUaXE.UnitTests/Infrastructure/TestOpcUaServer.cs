using Opc.Ua;
using Opc.Ua.Server;

namespace OpcUaXE.UnitTests.Infrastructure;

/// <summary>
/// In-process OPC UA server used exclusively for integration testing.
/// Starts on <see cref="TestNodeIds.EndpointUrl"/> with no message security
/// so that tests run without certificate trust issues.
/// </summary>
internal sealed class TestOpcUaServer : StandardServer
{
    internal TestNodeManager? TestNodes { get; private set; }

    protected override MasterNodeManager CreateMasterNodeManager(
        IServerInternal server, ApplicationConfiguration configuration)
    {
        TestNodes = new TestNodeManager(server, configuration);
        INodeManager[] nodeManagers = [TestNodes];
        return new MasterNodeManager(server, configuration, null, nodeManagers);
    }
}
