using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SolidFireGateway
{
    /// <summary>
    /// Represents a QoS policy as returned by SolidFire API.
    /// </summary>
    public class QosPolicy
    {
        public string name { get; set; }
        public Qos qos { get; set; }
        public int qosPolicyID { get; set; }
        public List<int> volumeIDs { get; set; }
    }

    /// <summary>
    /// Wrapper for ListQoSPolicies JSON-RPC result.
    /// </summary>
    public class ListQoSPoliciesResult
    {
        public List<QosPolicy> qosPolicies { get; set; }
    }

    /// <summary>
    /// Wrapper for GetQoSPolicy JSON-RPC result.
    /// </summary>
    public class GetQoSPolicyResult
    {
        public QosPolicy qosPolicy { get; set; }
    }

    /// <summary>
    /// Parameters for ModifyQoSPolicy JSON-RPC call.
    /// </summary>
    public class ModifyQoSPolicyParams
    {
        /// <summary>ID of the QoS policy to modify.</summary>
        public int qosPolicyID { get; set; }

        /// <summary>New name for the QoS policy.</summary>
        public string name { get; set; }

        /// <summary>New QoS settings.</summary>
        public QosUpdate qos { get; set; }
    }

    /// <summary>
    /// QoS update settings for ModifyQoSPolicy.
    /// </summary>
    public class QosUpdate
    {
        public int minIOPS { get; set; }
        public int maxIOPS { get; set; }
        public int burstIOPS { get; set; }
    }

    /// <summary>
    /// Wrapper for ModifyQoSPolicy JSON-RPC result.
    /// </summary>
    public class ModifyQoSPolicyResult
    {
        public QosPolicy qosPolicy { get; set; }
    }
}
