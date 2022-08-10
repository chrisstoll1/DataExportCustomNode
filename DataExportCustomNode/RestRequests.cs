using System;
using System.Net.Http;
using System.Reflection;
using System.Web;

namespace DataExportCustomNode
{
    public class RestRequests
    {
        public HttpClient Client;
        public string Token = Guid.Empty.ToString();
        // Any Square 9 API requests that require a token use an empty GUID. This is a requirement to use the workflow engine's API client.

        public RestRequests(HttpClient client)
        {
            Client = client;
            Client.DefaultRequestHeaders.Add("User-Agent", $"DataExport/{Assembly.GetExecutingAssembly().GetName().Version}");
        }

        /// <summary>
        /// Gets a document's secure ID. Requires API Full Access permissions in order to work.
        /// </summary>
        /// <param name="DatabaseID"></param>
        /// <param name="ArchiveID"></param>
        /// <param name="DocumentID"></param>
        /// <param name="Token"></param>
        /// <returns></returns>
        public string GetDocumentSecureID(int DatabaseID, int ArchiveID, int DocumentID)
        {
            var builder = new UriBuilder(Client.BaseAddress + $"dbs/{DatabaseID}/archives/{ArchiveID}");
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["DocumentID"] = DocumentID.ToString();
            query["token"] = this.Token;
            builder.Query = query.ToString();
            var requestUrl = builder.ToString();

            var response = Client.GetAsync(requestUrl).Result;
            var result = response.Content.ReadAsStringAsync().Result;
            if (response.IsSuccessStatusCode)
            {
                return result;
            }
            else
            {
                throw new Exception("Unable to get Document Secure ID: " + result);
            }
        }

        /// <summary>
        /// Retrieves a document from an Archive.
        /// </summary>
        /// <param name="DatabaseID"></param>
        /// <param name="ArchiveID"></param>
        /// <param name="DocumentID"></param>
        /// <param name="Token"></param>
        /// <param name="SecureID"></param>
        public string GetDocument(int DatabaseID, int ArchiveID, int DocumentID, string SecureID)
        {
            var builder = new UriBuilder(Client.BaseAddress + $"dbs/{DatabaseID}/archives/{ArchiveID}/documents/{DocumentID}");
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["token"] = this.Token;
            query["Secureid"] = SecureID;
            builder.Query = query.ToString();
            var requestUrl = builder.ToString();

            var response = Client.GetAsync(requestUrl).Result;
            var result = response.Content.ReadAsStringAsync().Result;
            if (response.IsSuccessStatusCode)
            {
                return result;
            }
            else
            {
                throw new Exception("Unable to get document: " + result);
            }
        }
    }
}
