namespace SolidFireGateway
{
    /// <summary>
    /// Parameters for GetVolume JSON-RPC call.
    /// </summary>
    public class GetVolumeParams
    {
        public int volumeID { get; set; }
    }

    /// <summary>
    /// Result object for GetVolume JSON-RPC response.
    /// </summary>
    public class GetVolumeResult
    {
        public Volume volume { get; set; }
    }
}
