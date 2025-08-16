using System;
#nullable enable
#pragma warning disable CS8618  // Non-nullable properties are initialized by JSON deserializer
using System.Collections.Generic;

namespace SolidFireGateway
{
    public class ListVolumesForAccountParams
    {
        public int accountID { get; set; }
        public bool includeVirtualVolumes { get; set; } = false;
    }

    public class Qos
    {
        public int burstIOPS { get; set; }
        /// <summary>
        /// Maximum burst IOPS permitted.
        /// </summary>
        public int burstTime { get; set; }
        /// <summary>
        /// Duration in seconds for QoS burst window (default 60s). 
		/// Back-end SolidFire API doesn't alow this valu to be changed.
        /// </summary>
        public Dictionary<string, int> curve { get; set; }
        public int maxIOPS { get; set; }
        public int minIOPS { get; set; }
    }

    /// <summary>
    /// Represents a SolidFire storage volume returned by the API.
    /// </summary>
    public class Volume
    {
        /// <summary>
        /// Access state of the volume (e.g. "readWrite" for an active volume on an active site).
        /// </summary>
        public string access { get; set; }

        /// <summary>
        /// ID of the account (tenant) that owns the volume.
        /// </summary>
        public int accountID { get; set; }

        /// <summary>
        /// Arbitrary key/value attributes for the volume (up to 10 pairs, JSON object up to 1000 bytes).
		/// It is suggested to allow 2-3 for volume user and reserve the rest for administrators.
        /// </summary>
        public Dictionary<string, object> attributes { get; set; }

        /// <summary>
        /// Block size for the volume in bytes.
		/// Should be 4096 at all times.
        /// </summary>
        public int blockSize { get; set; }

        /// <summary>
        /// UTC timestamp when the volume was created, in ISO 8601 format.
        /// </summary>
        public string createTime { get; set; }

        /// <summary>
        /// Current protection scheme applied to the volume. 
		/// Always double helix except in Element Demo VM.
        /// </summary>
        public string currentProtectionScheme { get; set; }

        /// <summary>
        /// UTC timestamp when the volume was deleted, if applicable, in ISO 8601 format.
		/// Add purge time to a non-null values to determine when a volume will be auto-purged.
        /// </summary>
        public string deleteTime { get; set; }

        /// <summary>
        /// Indicates if the volume is using 512e (512-byte emulation) format.
		/// 512 emulation is used if Enable512e is true, and 4096 if false.
		/// Windows OS supports 4096 and that is our recommended setting for Windows and Hyper-V.
        /// </summary>
        public bool enable512e { get; set; }

        /// <summary>
        /// Indicates if SnapMirror replication is enabled for the volume. 
		/// It is recommended to avoid using this feature.
        /// </summary>
        public bool enableSnapMirrorReplication { get; set; }

        /// <summary>
        /// Maximum number of snapshots to maintain when using First-In-First-Out (FIFO) retention mode.
        /// </summary>
        public int fifoSize { get; set; }

        /// <summary>
        /// Specifies the minimum number of snapshots reserved for First-In-First-Out (FIFO) retention mode.
        /// </summary>
        public int minFifoSize { get; set; }

        /// <summary>
        /// The UTC+0 formatted time the volume was purged from the system, in ISO 8601 format.
        /// </summary>
        public string purgeTime { get; set; }

        /// <summary>
        /// The last time any access (including I/O) to the volume occurred, formatted as UTC+0 ISO 8601 string. Null if unknown.
        /// </summary>
        public string lastAccessTime { get; set; }

        /// <summary>
        /// The last time any I/O occurred on the volume, formatted as UTC+0 ISO 8601 string. Null if unknown.
        /// </summary>
        public string lastAccessTimeIO { get; set; }

        /// <summary>
        /// Name of the volume. Can't begin with a digit. Alphanumeric only, no special characters, but we don't validate that here.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Previous protection scheme applied to the volume, if applicable.
        /// </summary>
        public string previousProtectionScheme { get; set; }

        /// <summary>
        /// Quality of Service (QoS) settings for the volume.
        /// </summary>
        public Qos qos { get; set; }

        /// <summary>
        /// ID of the QoS policy applied to the volume, if any. 
		/// QoSPolicyID is the only supported setting for storage quality-of-service on SolidFire WAC Gateawy.
        /// </summary>
        public int? qosPolicyID { get; set; }

        /// <summary>
        /// SCSI EUI-64 Device ID for the volume.
        /// </summary>
        public string scsiEUIDeviceID { get; set; }

        /// <summary>
        /// SCSI NAA Device ID for the volume.
        /// </summary>
        public string scsiNAADeviceID { get; set; }

        /// <summary>
        /// Number of slices in the volume.
        /// </summary>
        public int sliceCount { get; set; }

        /// <summary>
        /// Status of the volume (e.g. "active", "deleted").
		///	Filter by this value to find usable volumes or estimate savings from volume purge. 
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// Total size of the volume in bytes.
        /// </summary>
        public long totalSize { get; set; }

        /// <summary>
        /// ID of the virtual volume, if this volume is a part of a virtual volume. SF WAC GW does not support virtual volumes.
        /// </summary>
        public object virtualVolumeID { get; set; }

        /// <summary>
        /// List of volume access group IDs that have access to this volume.
		/// SolidFire WAC Gateawy recommends the use of CHAP over VAG.		
        /// </summary>
        public List<int> volumeAccessGroups { get; set; }

        /// <summary>
        /// UUID of the consistency group that this volume belongs to, if applicable.
        /// </summary>
        public string volumeConsistencyGroupUUID { get; set; }

        /// <summary>
        /// Unique ID of the volume.
		/// Always rely on volumeID and never on name which can be duplicated.
        /// </summary>
        public int volumeID { get; set; }

        /// <summary>
        /// iSCSI Qualified Name for the volume.
        /// </summary>
        public string iqn { get; set; }

        /// <summary>
        /// List of volume pair objects, if the volume is part of a volume pair.		
        /// </summary>
        public List<object> volumePairs { get; set; }

        /// <summary>
        /// UUID of the volume.
        /// </summary>
        public string volumeUUID { get; set; }
    }

    public class ListVolumesForAccountResult
    {
        public List<Volume> volumes { get; set; }
    }

    public class ListVolumesForAccountResponse
    {
        public int id { get; set; }
        public string cluster { get; set; }
        public ListVolumesForAccountResult result { get; set; }
    }
}
