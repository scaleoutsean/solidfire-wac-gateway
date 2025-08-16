using System.Collections.Generic;

namespace SolidFireGateway
{
    /// <summary>
    /// Parameters for CreateSnapshot JSON-RPC call.
    /// </summary>
    public class CreateSnapshotParams
    {
        public int volumeID { get; set; }
        public string retention { get; set; }
        public Dictionary<string, object> attributes { get; set; }
    }

    /// <summary>
    /// Snapshot object returned by API.
    /// </summary>
    public class Snapshot
    {
        public Dictionary<string, object> attributes { get; set; }
        public string checksum { get; set; }
        public string createTime { get; set; }
        public bool enableRemoteReplication { get; set; }
        public string expirationReason { get; set; }
        public string expirationTime { get; set; }
        public int groupID { get; set; }
        public string groupSnapshotUUID { get; set; }
        public string instanceCreateTime { get; set; }
        public string instanceSnapshotUUID { get; set; }
        public string name { get; set; }
        public string snapMirrorLabel { get; set; }
        public int snapshotID { get; set; }
        public string snapshotUUID { get; set; }
        public string status { get; set; }
        public long totalSize { get; set; }
        public object virtualVolumeID { get; set; }
        public int volumeID { get; set; }
        public string volumeName { get; set; }
    }

    /// <summary>
    /// Result wrapper for CreateSnapshot JSON-RPC call.
    /// </summary>
    public class CreateSnapshotResult
    {
        public string checksum { get; set; }
        public Snapshot snapshot { get; set; }
        public int snapshotID { get; set; }
    }

    /// <summary>
    /// Parameters for CreateGroupSnapshot JSON-RPC call.
    /// </summary>
    public class CreateGroupSnapshotParams
    {
        public List<int> volumes { get; set; }
        public string retention { get; set; }
        public Dictionary<string, object> attributes { get; set; }
    }

    /// <summary>
    /// Group snapshot object returned by API.
    /// </summary>
    public class GroupSnapshot
    {
        public Dictionary<string, object> attributes { get; set; }
        public string createTime { get; set; }
        public bool enableRemoteReplication { get; set; }
        public int groupSnapshotID { get; set; }
        public string groupSnapshotUUID { get; set; }
        public List<Snapshot> members { get; set; }
        public string name { get; set; }
        public string status { get; set; }
    }

    /// <summary>
    /// Member summary for CreateGroupSnapshotResult.
    /// </summary>
    public class GroupSnapshotMember
    {
        public string checksum { get; set; }
        public int snapshotID { get; set; }
        public string snapshotUUID { get; set; }
        public int volumeID { get; set; }
    }

    /// <summary>
    /// Result wrapper for CreateGroupSnapshot JSON-RPC call.
    /// </summary>
    public class CreateGroupSnapshotResult
    {
        public GroupSnapshot groupSnapshot { get; set; }
        public int groupSnapshotID { get; set; }
        public List<GroupSnapshotMember> members { get; set; }
    }

    /// <summary>
    /// Parameters for ListGroupSnapshots JSON-RPC call.
    /// </summary>
    public class ListGroupSnapshotsParams
    {
        public List<int> volumes { get; set; }
    }

    /// <summary>
    /// Result wrapper for ListGroupSnapshots JSON-RPC call.
    /// </summary>
    public class ListGroupSnapshotsResult
    {
        public List<GroupSnapshot> groupSnapshots { get; set; }
    }

    /// <summary>
    /// Parameters for ListSnapshots JSON-RPC call.
    /// </summary>
    public class ListSnapshotsParams
    {
        public int volumeID { get; set; }
    }

    /// <summary>
    /// Result wrapper for ListSnapshots JSON-RPC call.
    /// </summary>
    public class ListSnapshotsResult
    {
        public List<Snapshot> snapshots { get; set; }
    }
}
