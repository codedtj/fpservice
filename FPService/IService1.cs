using System.ServiceModel;
using System.ServiceModel.Web;

namespace FPService
{
    [ServiceContract]
    public interface IService1
    {
        [OperationContract]
        [FaultContract(typeof(string))]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Wrapped,
            Method = "GET", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json,
            UriTemplate = "/hello")]
        string Hello();


        [OperationContract]
        [FaultContract(typeof(string))]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Wrapped,
            Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json,
            UriTemplate = "z/parallel/identify_user_wide")]
        int ZkIdentifierWide(string print, string query, string connectionString, int rate);

        [OperationContract]
        [FaultContract(typeof(string))]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Wrapped,
            Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json,
            UriTemplate = "z/identify_user")]
        int ZkIdentifier(string connectionString, string query, string printFieldName,
           string idFieldName, int rate, string print);

        [OperationContract]
        [FaultContract(typeof(string))]
        [WebInvoke(BodyStyle = WebMessageBodyStyle.Wrapped,
            Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json,
            UriTemplate = "z/match_prints")]
        int MatchPrints(string fp1, string fp2);
    }   
}
