using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace SolidFireGateway
{
    /// <summary>
    /// Parameters for CreateVolume JSON-RPC call.
    /// </summary>
    public class CreateVolumeParams
    {
        public string name { get; set; }
        public int accountID { get; set; }
        public long totalSize { get; set; }
        /// <summary>
        /// Whether to enable 512e on the volume.
        /// </summary>
        public bool enable512e { get; set; }
        /// <summary>
        /// Arbitrary key/value attributes for the volume (up to 3 pairs).
        /// Note: Exceeding this limit may result in older attributes being overwritten or dropped.
        /// </summary>
        public Dictionary<string, object> attributes { get; set; } = new Dictionary<string, object>();
        /// <summary>
        /// QoS policy ID to apply to the volume.
        /// </summary>
        /// <summary>
        /// QoS policy ID to apply to the volume.
        /// </summary>
        public int qosPolicyID { get; set; }
        /// <summary>
        /// Always associate the volume with its QoS policy.
        /// </summary>
        public bool associateWithQoSPolicy { get; set; } = true;
        [JsonIgnore]
        public bool enableSnapMirrorReplication { get; set; }
        [JsonIgnore]
        public int fifoSize { get; set; }
        [JsonIgnore]
        public int minFifoSize { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CreateVolumeQos? qos { get; set; }
    }

    /// <summary>
    /// Result object for CreateVolume JSON-RPC response.
    /// </summary>
    public class CreateVolumeResult
    {
        public Dictionary<string, int> curve { get; set; }
        public Volume volume { get; set; }
        public int volumeID { get; set; }
    }

    /// <summary>
    /// Wrapper for CreateVolume API response.
    /// </summary>
    public class CreateVolumeResponse
    {
        public int id { get; set; }
        public CreateVolumeResult result { get; set; }
    }
}

    /// <summary>
    /// QoS parameters for CreateVolume API call (excludes burstTime and curve).
    /// </summary>
    public class CreateVolumeQos
    {
        /// <summary>
        /// Maximum IOPS during burst.
        /// </summary>
        public int burstIOPS { get; set; }
        /// <summary>
        /// Maximum steady-state IOPS.
        /// </summary>
        public int maxIOPS { get; set; }
        /// <summary>
        /// Minimum steady-state IOPS.
        /// </summary>
        public int minIOPS { get; set; }
    }
