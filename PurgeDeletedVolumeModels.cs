namespace SolidFireGateway
{
    /// <summary>
    /// Parameters for PurgeDeletedVolume JSON-RPC call.
    /// </summary>
    public class PurgeDeletedVolumeParams
    {
        public int volumeID { get; set; }
    }

    /// <summary>
    /// Response wrapper for PurgeDeletedVolume API; result is empty.
    /// </summary>
    public class PurgeDeletedVolumeResponse
    {
        public int id { get; set; }
        public object result { get; set; } = new { };
    }
}
