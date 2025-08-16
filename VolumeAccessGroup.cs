using System.Collections.Generic;

namespace SolidFireGateway
{
    /// <summary>
    /// Represents a Volume Access Group in the SolidFire API.
    /// At least one of Initiators or Volumes must be non-null.
    /// </summary>
    public class VolumeAccessGroup
    {
        /// <summary>
        /// List of name-value attributes.
        /// </summary>
        public Dictionary<string, object> attributes { get; set; }

        /// <summary>
        /// Array of volumes that have been deleted from the group but not yet purged.
        /// </summary>
        public List<int> deletedVolumes { get; set; }

        /// <summary>
        /// A list of IDs of initiators mapped to the volume access group.
        /// </summary>
        public List<int> initiatorIDs { get; set; }

        /// <summary>
        /// Array of unique IQN or WWPN initiators mapped to the volume access group.
        /// </summary>
        public List<string> initiators { get; set; }

        /// <summary>
        /// Name of the volume access group.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Unique identifier for the volume access group.
        /// </summary>
        public int volumeAccessGroupID { get; set; }

        /// <summary>
        /// A list of VolumeIDs belonging to the volume access group.
        /// </summary>
        public List<int> volumes { get; set; }
    }
}
