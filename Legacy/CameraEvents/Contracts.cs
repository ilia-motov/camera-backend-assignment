using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace CameraEvents
{
    [ServiceContract]
    public interface ICameraService
    {
        [OperationContract]
        CameraEvent[] GetEvents(DateTime from, DateTime to);
    }

    [DataContract]
    public class CameraEvent
    {
        [DataMember]
        public int CameraId { get; set; }
        [DataMember]
        public DateTime Timestamp { get; set; }
        [DataMember]
        public double Confidence { get; set; }
    }
}
