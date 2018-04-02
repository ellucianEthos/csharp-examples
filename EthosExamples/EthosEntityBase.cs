using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EthosExamples
{
    /// <summary>
    /// Base class for Ethos entity models
    /// </summary>
    public abstract class EthosEntityBase
    {

        /// <summary>
        /// Content-Type value in header for this resource. Defaults to integration schema if not overriden
        /// </summary>
        [JsonIgnore]
        public virtual string HeaderContentType
        {
            get
            {
                return "application/vnd.hedtech.integration.v2+json";
            }
        }
        /// <summary>
        /// The name of the Ethos entity
        /// </summary>
        /// <returns></returns>
        [JsonIgnore]
        abstract public string ResourcePluralName { get; }


        /// <summary>
        /// The metadata for this entity instance
        /// If the derived class should not serialize a "metadata" attribute, you can add this property to the
        /// class with a 'new' identifier, and implement the ShouldSerialize method
        /// </summary>
        [JsonProperty(PropertyName = "metadata")]
        public EthosMetadata Metadata { get; set; }

        /// <summary>
        /// The Ethos Id of this entity instance.
        /// If the derived class should not serialize an "id" attribute, you can add this property to the
        /// class with a 'new' identifier, and implement the ShouldSerialize method
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

    }

    /// <summary>
    /// Entity metadata required on all entities in Ethos
    /// </summary>
    public class EthosMetadata
    {
        /// <summary>
        /// The name of the originator of the data
        /// </summary>
        [JsonProperty(PropertyName = "createdBy", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
        public string CreatedBy { get; set; }

        /// <summary>
        /// The date and time when the entity instance was created
        /// </summary>
        [JsonProperty(PropertyName = "createdOn", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? CreatedOn { get; set; }

        /// <summary>
        /// The name of the modifier of the data
        /// </summary>
        [JsonProperty(PropertyName = "modifiedBy", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
        public string ModifiedBy { get; set; }

        /// <summary>
        /// The date and time when the entity instance was modified
        /// </summary>
        [JsonProperty(PropertyName = "modifiedOn", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? ModifiedOn { get; set; }
    }
}
