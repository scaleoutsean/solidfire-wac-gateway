using System;
using System.Collections.Generic;

namespace SolidFireGateway
{
    /// <summary>
    /// Result object for GetAccountEfficiency JSON-RPC call.
    /// </summary>
    public class AccountEfficiencyResult
    {
        /// <summary>Compression ratio for the account.</summary>
        public double compression { get; set; }
        /// <summary>Deduplication ratio for the account.</summary>
        public double deduplication { get; set; }
        /// <summary>Thin provisioning ratio for the account.</summary>
        public double thinProvisioning { get; set; }
        /// <summary>List of missing volume IDs, if any.</summary>
        public List<int> missingVolumes { get; set; }
        /// <summary>Timestamp of the metrics in ISO 8601 format.</summary>
        public string timestamp { get; set; }
    }

    /// <summary>
    /// Represents volume statistics for an account.
    /// </summary>
    public class VolumeStats
    {
        public int accountID { get; set; }
        public long nonZeroBlocks { get; set; }
        public long readBytes { get; set; }
        public long readBytesLastSample { get; set; }
        public long readOps { get; set; }
        public long readOpsLastSample { get; set; }
        public long samplePeriodMSec { get; set; }
        public string timestamp { get; set; }
        public long unalignedReads { get; set; }
        public long unalignedWrites { get; set; }
        public List<int> volumeAccessGroups { get; set; }
        public int volumeID { get; set; }
        public long volumeSize { get; set; }
        public long writeBytes { get; set; }
        public long writeBytesLastSample { get; set; }
        public long writeOps { get; set; }
        public long writeOpsLastSample { get; set; }
        public long zeroBlocks { get; set; }
    }

    /// <summary>
    /// Result wrapper for ListVolumeStatsByAccount JSON-RPC call.
    /// </summary>
    public class ListVolumeStatsByAccountResult
    {
        public List<VolumeStats> volumeStats { get; set; }
    }
}
    /// <summary>
    /// Represents a SolidFire account returned by the API.
    /// </summary>
    public class Account
    {
        public int accountID { get; set; }
        public Dictionary<string, object> attributes { get; set; }
        public bool enableChap { get; set; }
        public string initiatorSecret { get; set; }
        public string targetSecret { get; set; }
        public string status { get; set; }
        public string storageContainerID { get; set; }
        public string username { get; set; }
        public List<int> volumes { get; set; }
    }

    /// <summary>
    /// Wrapper for ListAccounts JSON-RPC result.
    /// </summary>
    public class ListAccountsResult
    {
        public List<Account> accounts { get; set; }
    }

    /// <summary>
    /// Wrapper for GetAccountByID JSON-RPC result.
    /// </summary>
    public class GetAccountResult
    {
        public Account account { get; set; }
    }
