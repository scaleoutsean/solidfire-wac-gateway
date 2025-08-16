namespace SolidFireGateway
{
    /// <summary>
    /// Parameters for DeleteVolume JSON-RPC call.
    /// </summary>
    public class DeleteVolumeParams
    {
        public int volumeID { get; set; }
    }

    /// <summary>
    /// Result for DeleteVolume JSON-RPC response.
    /// </summary>
    public class DeleteVolumeResult
    {
        public Volume volume { get; set; }
    }
}
