using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EthosExamples
{
    public enum ChangeOperation { Create, Update, Delete };

    /// <summary>
    /// Represents a change notification for an authoritative source to publish to the hub
    /// Not a normal EthosEntity because it cannot include Metadata object.
    /// </summary>
    public class ChangeNotificationV2
    {

        /// <summary>
        /// Content-Type value in header for this resource
        /// </summary>
        public const string HeaderContentType = "application/vnd.hedtech.change-notifications.v2+json";

        /// <summary>
        /// Magic string for Ethos content-type
        /// </summary>
        public const string ResourceRepresentationContentType = "resource-representation";

        /// <summary>
        /// Magic string for empty (deleted) content-type
        /// </summary>
        public const string EmptyContentType = "empty";

        /// <summary>
        /// Magic string for create operations
        /// </summary>
        public const string CreatedOperation = "created";

        /// <summary>
        /// Magic string for update operations
        /// </summary>
        public const string ReplacedOperation = "replaced";

        /// <summary>
        /// Magic string for delete operations
        /// </summary>
        public const string DeletedOperation = "deleted";




        /// <summary>
        /// Default constructor assumes content type is resource-representation
        /// </summary>
        public ChangeNotificationV2()
        {
            ContentType = ResourceRepresentationContentType;
        }

        /// <summary>
        /// Helper constructor populates properties based on ethos entity object, assuming resource-representation contentType.
        /// </summary>
        /// <param name="ethosEntity">Model representation of ethos entity</param>
        /// <param name="operation">Operation that was performed in CRM</param>
        public ChangeNotificationV2(EthosEntityBase ethosEntity, string operation)
        {

            Resource = new ChangeNotificationV2.EthosResource(ethosEntity);
            ID = -1; //cannot be null, but does not matter on publish
            Operation = operation;
            if (operation.Equals(DeletedOperation))
            {
                ContentType = EmptyContentType;
                Content = new JObject();
            }
            else
            {
                Content = JObject.Parse(JsonConvert.SerializeObject(ethosEntity));
                ContentType = ResourceRepresentationContentType;
            }
        }

        /// <summary>
        /// Helper constructor populates properties based on ethos entity object, assuming resource-representation contentType.
        /// </summary>
        /// <param name="ethosEntity">Model representation of ethos entity</param>
        /// <param name="operation">Operation that was performed in CRM</param>
        public ChangeNotificationV2(EthosEntityBase ethosEntity, ChangeOperation operation) : this(ethosEntity, GetAction(operation))
        { }

        private static string GetAction(ChangeOperation action)
        {
            switch (action)
            {
                case ChangeOperation.Create:
                    return ChangeNotificationV2.CreatedOperation.ToString();
                case ChangeOperation.Update:
                    return ChangeNotificationV2.ReplacedOperation.ToString();
                case ChangeOperation.Delete:
                    return ChangeNotificationV2.DeletedOperation.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// Id of this change notification
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public int ID { get; set; }

        /// <summary>
        /// The datetime of the publish message
        /// </summary>
        [JsonProperty(PropertyName = "published")]
        public DateTime Published { get; set; }

        /// <summary>
        /// The type of the Content property. One of { resource-representation, empty, patch, partial, limited }
        /// </summary>
        [JsonProperty(PropertyName = "contentType")]
        public string ContentType { get; set; }

        /// <summary>
        /// Action performed on the resource. One of { created, replaced, patched, deleted }
        /// </summary>
        [JsonProperty(PropertyName = "operation")]
        public string Operation { get; set; }

        /// <summary>
        /// Representation of change. For resource-representation, use full resource object.
        /// </summary>
        [JsonProperty(PropertyName = "content")]
        public JObject Content { get; set; }

        /// <summary>
        /// Summary of the affected resource
        /// </summary>
        [JsonProperty(PropertyName = "resource")]
        public EthosResource Resource { get; set; }

        /// <summary>
        /// Describes a resource that has been changed
        /// /// </summary>
        public class EthosResource
        {
            /// <summary>
            /// Pluralized name of the affected resource
            /// </summary>
            /// <value>
            ///  The Name.
            /// </value>
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            /// <summary>
            /// Unique identifier of the resource
            /// </summary>
            /// <value>
            ///  The Id.
            /// </value>
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            /// <summary>
            /// Version of the resource
            /// </summary>
            /// <value>
            ///  The Version.
            /// </value>
            [JsonProperty(PropertyName = "version")]
            public string Version { get; set; }

            /// <summary>
            /// Create a Resource object based on the ethos entity
            /// </summary>
            /// <param name="entity"></param>
            public EthosResource(EthosEntityBase entity)
            {
                if (entity != null)
                {
                    this.Name = entity.ResourcePluralName;
                    this.Id = entity.Id.ToString();
                    this.Version = entity.HeaderContentType;
                }
            }
        }
    }
}
