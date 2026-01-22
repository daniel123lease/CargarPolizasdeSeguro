using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Xml;

namespace CargaMasivaDePolizasDeSeguro
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string b64 = "";
            //string fullPath = ";
            string FILE64;
            int ID_COTIZACION;
            int ID_DOCUMENTO = 215;
            int ID_CARATULA;
            int ID_TIPO_ARCHIVO = 3;

            /*Ruta donde se guardan los PDF que se van a cargar*/
            var files = Directory.GetFiles(@"C:\testborrar\PolizasDeSeguro");

            List<string> filesList = (from f in files
                                      let fileInfo = new FileInfo(f)
                                      select f).ToList();


            foreach (string item in filesList)
            {
                FILE64 = FileToBase64(item);

                /*CUANDO EL PDF TIENE EL ID_COTIZACION COMO NOMBRE*/

                // ID_COTIZACION = Convert.ToInt32(Path.GetFileName(item).Replace(".pdf", ""));
                //  ID_CARATULA = GetIdCaratula(Convert.ToInt32(Path.GetFileName(item).Replace(".pdf", "")));

                /*CUANDO EL PDF TIENE  COMO NOMBRE POLIZA 012336554 (LA PALABRA POLIZA Y EL NUMERO DE POLIZA)*/

                ID_COTIZACION = GetIdCotizacionByNumPoliza(Path.GetFileName(item).Replace("POLIZA ", "").Replace(".pdf", ""));
                ID_CARATULA = GetIdCaratula(ID_COTIZACION);


                if (ID_CARATULA > 0)
                {
                    CallWebService(FILE64, ID_COTIZACION, ID_DOCUMENTO, ID_CARATULA, ID_TIPO_ARCHIVO);

                    Console.WriteLine("cargada cotizacion " + ID_COTIZACION.ToString() + " poliza--" + item);
                }

            }
            Console.WriteLine("****************DONE!!!!*****************");
            Console.ReadLine();
        }


        #region CallWS
        private static HttpWebRequest CreateWebRequest(string url, string action)
        {
            try
            {
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.Headers.Add("SOAPAction", action);
                webRequest.ContentType = "text/xml;charset=\"utf-8\"";
                webRequest.Accept = "text/xml";
                webRequest.Method = "POST";
                return webRequest;
            }
            catch (Exception ex)
            {
                WriteToFile("Error CreateWebRequest: " + ex.Message);
                throw;
            }
        }
        private static int GetIdCaratula(int ID_COTIZACION)
        {
            int ID_CARATULA = 0;
            try
            {
                string conn = ConfigurationManager.ConnectionStrings["DbContext123"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(conn))
                {
                    connection.Open();
                    String query = "select ISNULL( ID_CARATULA,0) AS ID_CARATULA from BD_CARATULA where id_cotizacion = " + ID_COTIZACION + " and tipo = 'deudor'";

                    SqlCommand command = new SqlCommand(query, connection);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ID_CARATULA = Convert.ToInt32(reader["ID_CARATULA"].ToString().Trim());
                        }
                    }

                }
                return ID_CARATULA;
            }
            catch
            {
                return -1;
            }
        }

        private static int GetIdCotizacionByNumPoliza(string NUM_POLIZA)
        {
            int ID_COTIZACION = 0;
            try
            {
                string conn = ConfigurationManager.ConnectionStrings["DbContext123"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(conn))
                {
                    connection.Open();
                    String query = "SELECT ISNULL(ID_COTIZACION,0) AS ID_COTIZACION FROM dbo.BD_COTIZACION WHERE NUM_POLIZA = '" + NUM_POLIZA + "'";

                    SqlCommand command = new SqlCommand(query, connection);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ID_COTIZACION = Convert.ToInt32(reader["ID_COTIZACION"].ToString().Trim());
                        }
                    }

                }
                return ID_COTIZACION;
            }
            catch
            {
                return -1;
            }
        }

        private static XmlDocument CreateSoapEnvelope(string FILE64, int ID_COTIZACION, int ID_DOCUMENTO, int ID_CARATULA, int ID_TIPO_ARCHIVO)
        {
            try
            {
                var bucketPath = WebConfigurationManager.AppSettings["bucketPath"];
                XmlDocument soapEnvelopeDocument = new XmlDocument();
                string xml = "";

                xml = " <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/' xmlns:tem='http://tempuri.org/'>" +
                        "    <soapenv:Header>" +
                        "       <tem:Credentials>" +
                        "          <tem:userName>adminBucket</tem:userName>" +
                        "          <tem:password>Pin3d0$2023</tem:password>" +
                        "       </tem:Credentials>" +
                        "    </soapenv:Header>" +
                        "    <soapenv:Body>" +
                        "       <tem:TransportFile>" +
                        "          <tem:ID_COTIZACION>" +
                        "             <tem:ID_COTIZACION>" + ID_COTIZACION + "</tem:ID_COTIZACION>" +
                        "             <tem:ID_DOCUMENTO>" + ID_DOCUMENTO + "</tem:ID_DOCUMENTO>" +
                        "             <tem:ID_CARATULA>" + ID_CARATULA + "</tem:ID_CARATULA>" +
                        "             <tem:ID_TIPO_ARCHIVO>" + ID_TIPO_ARCHIVO + "</tem:ID_TIPO_ARCHIVO>" +
                        "             <tem:FILE64>" + FILE64 + "</tem:FILE64>" +
                        "          </tem:ID_COTIZACION>" +
                        "       </tem:TransportFile>" +
                        "    </soapenv:Body>" +
                        " </soapenv:Envelope>";



                soapEnvelopeDocument.LoadXml(xml);
                return soapEnvelopeDocument;
            }
            catch (Exception ex)
            {
                WriteToFile("Error CreateSoapEnvelope: " + ex.Message);
                throw;
            }
        }

        private static void InsertSoapEnvelopeIntoWebRequest(XmlDocument soapEnvelopeXml, HttpWebRequest webRequest)
        {
            try
            {
                using (Stream stream = webRequest.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }
            }
            catch (Exception ex)
            {
                WriteToFile("Error InsertSoapEnvelopeIntoWebRequest: " + ex.Message);
                throw;
            }
        }
        private static void CallWebService(string FILE64, int ID_COTIZACION, int ID_DOCUMENTO, int ID_CARATULA, int ID_TIPO_ARCHIVO)
        {
            try
            {
                var _url = WebConfigurationManager.AppSettings["urlBucketWS"];
                var _action = WebConfigurationManager.AppSettings["actionUploadBucketFileWS"];

                XmlDocument soapEnvelopeXml = CreateSoapEnvelope(FILE64, ID_COTIZACION, ID_DOCUMENTO, ID_CARATULA, ID_TIPO_ARCHIVO);
                HttpWebRequest webRequest = CreateWebRequest(_url, _action);
                InsertSoapEnvelopeIntoWebRequest(soapEnvelopeXml, webRequest);

                // begin async call to web request.
                IAsyncResult asyncResult = webRequest.BeginGetResponse(null, null);

                // suspend this thread until call is complete. You might want to
                // do something usefull here like update your UI.
                asyncResult.AsyncWaitHandle.WaitOne();

                // get the response from the completed web request.
                string soapResult;
                using (WebResponse webResponse = webRequest.EndGetResponse(asyncResult))
                {
                    using (StreamReader rd = new StreamReader(webResponse.GetResponseStream()))
                    {
                        soapResult = rd.ReadToEnd();
                    }

                }

                WriteToFile("Archivo  se cargo al bucket exitosamente");
            }
            catch (Exception ex)
            {
                WriteToFile("Error CallWebService: " + ex.Message);
                throw;
            }
        }

        #endregion

        public static void WriteToFile(string Message)
        {
            try
            {
                string loglPath = WebConfigurationManager.AppSettings["logPath"];
                string path = loglPath;
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                string filepath = loglPath + "\\ServiceLogArchivosCargaBuro_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
                if (!File.Exists(filepath))
                {
                    using (StreamWriter sw = File.CreateText(filepath))
                    {
                        sw.WriteLine(Message);
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(filepath))
                    {
                        sw.WriteLine(Message);
                    }
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }
        public static string FileToBase64(string sPath)
        {
            try
            {
                string base64FileRepresentation = "";
                if (File.Exists(sPath))
                {

                    byte[] fileArray = File.ReadAllBytes(sPath);
                    base64FileRepresentation = Convert.ToBase64String(fileArray);

                }
                else
                {
                    base64FileRepresentation = "Error: No se encontro el documento en la ruta.";
                }
                return base64FileRepresentation;
            }
            catch (Exception ex)
            {
                WriteToFile("Error FileToBase64: " + ex.Message);
                throw;
            }
        }
    }
}
