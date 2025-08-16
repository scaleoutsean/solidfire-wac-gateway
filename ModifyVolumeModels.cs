using System.Collections.Generic;

namespace SolidFireGateway
{
    /// <summary>
    /// Parameters for ModifyVolume JSON-RPC call.
    /// </summary>
    public class ModifyVolumeParams
    {
        /// <summary>Unique ID of the volume to modify.</summary>
        public int volumeID { get; set; }

        /// <summary>The new access state for the volume (e.g., "readOnly", "readWrite").</summary>
        public string? access { get; set; }

        /// <summary>Custom key/value attributes to apply (up to 3 pairs).</summary>
        /// <remarks>Note: This gateway currently limits updates to 3 attributes; additional existing attributes may be retained or overwritten by storage cluster admins.</remarks>
        public Dictionary<string, object>? attributes { get; set; }

        /// <summary>ID of a QoS policy to associate (optional).</summary>
        public int? qosPolicyID { get; set; }

        /// <summary>The new total size of the volume in bytes (grow only, no shrink).</summary>
        public long? totalSize { get; set; }
    }

    /// <summary>
    /// Result object for ModifyVolume JSON-RPC response.
    /// </summary>
    public class ModifyVolumeResult
    {
        /// <summary>Modified volume object returned by API.</summary>
        public Volume volume { get; set; }
    }

    /// <summary>
    /// Wrapper for ModifyVolume API response.
    /// </summary>
    public class ModifyVolumeResponse
    {
        public int id { get; set; }
        public ModifyVolumeResult result { get; set; }
    }
}
