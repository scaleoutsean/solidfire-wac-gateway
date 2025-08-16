namespace SolidFireGateway
{
    public class ClusterInfo
    {
        /// <summary>
        /// List of name-value pairs in JSON object format.
        /// </summary>
        public Dictionary<string, object>? attributes { get; set; }

        /// <summary>
        /// The protection scheme used by default for new volumes.
        /// </summary>
        public string? defaultProtectionScheme { get; set; }

        /// <summary>
        /// A list of all protection schemes that have been enabled on this storage cluster.
        /// </summary>
        public string[]? enabledProtectionSchemes { get; set; }

        /// <summary>
        /// The state of the Encryption at Rest feature.
        /// Possible values: Enabling, Enabled, Disabling, Disabled.
        /// </summary>
        public string? encryptionAtRestState { get; set; }

        /// <summary>
        /// The nodes that are participating in the cluster.
        /// </summary>
        public string[]? ensemble { get; set; }

        /// <summary>
        /// The floating (virtual) IP address for the cluster on the management network.
        /// </summary>
        public string? mvip { get; set; }

        /// <summary>
        /// The physical interface associated with the MVIP address.
        /// </summary>
        public string? mvipInterface { get; set; }

        /// <summary>
        /// The node that holds the master MVIP address.
        /// </summary>
        public int? mvipNodeID { get; set; }

        /// <summary>
        /// The VLAN identifier for the MVIP address.
        /// </summary>
        public string? mvipVlanTag { get; set; }

        /// <summary>
        /// The unique cluster name.
        /// </summary>
        public string? name { get; set; }

        /// <summary>
        /// The number of replicas of each piece of data to store in the cluster. Should be fixed to 2 for SolidFire except in Demo VM (1).
        /// </summary>
        public int? repCount { get; set; }

        /// <summary>
        /// Software-based encryption-at-rest state.
        /// </summary>
        public string? softwareEncryptionAtRestState { get; set; }

        /// <summary>
        /// A list of all protection schemes that are supported on this storage cluster.
        /// </summary>
        public string[]? supportedProtectionSchemes { get; set; }

        /// <summary>
        /// The floating (virtual) IP address for the cluster on the storage (iSCSI) network.
        /// </summary>
        public string? svip { get; set; }

        /// <summary>
        /// The physical interface associated with the master SVIP address.
        /// </summary>
        public string? svipInterface { get; set; }

        /// <summary>
        /// The node holding the master SVIP address.
        /// </summary>
        public int? svipNodeID { get; set; }

        /// <summary>
        /// The VLAN identifier for the master SVIP address.
        /// </summary>
        public string? svipVlanTag { get; set; }

        /// <summary>
        /// The unique ID for the cluster.
        /// </summary>
        public string? uniqueID { get; set; }

        /// <summary>
        /// The universally unique ID of the cluster.
        /// </summary>
        public string? uuid { get; set; }
    }

    public class ClusterInfoResult
    {
        public ClusterInfo? clusterInfo { get; set; }
    }

    public class ClusterInfoResponse
    {
        public int id { get; set; }
        public string? cluster { get; set; } // Name of the cluster
        public ClusterInfoResult? result { get; set; }
    }
}
