//------------------------------------------------------------------------------
// <auto-generated>
//     Dieser Code wurde von einem Tool generiert.
//     Laufzeitversion:4.0.30319.42000
//
//     Änderungen an dieser Datei können falsches Verhalten verursachen und gehen verloren, wenn
//     der Code erneut generiert wird.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AzureDataSender_FEZ
{
    
    internal partial class Resources
    {
        private static System.Resources.ResourceManager manager;
        internal static System.Resources.ResourceManager ResourceManager
        {
            get
            {
                if ((Resources.manager == null))
                {
                    Resources.manager = new System.Resources.ResourceManager("AzureDataSender_FEZ.Resources", typeof(Resources).Assembly);
                }
                return Resources.manager;
            }
        }
        internal static byte[] GetBytes(Resources.BinaryResources id)
        {
            return ((byte[])(ResourceManager.GetObject(((short)(id)))));
        }
        [System.SerializableAttribute()]
        internal enum BinaryResources : short
        {
            DigiCert_Baltimore_Root = -10335,
            DigiCertGlobalRootCA = 1766,
            Digicert___StackExchange = 4549,
            Digicert___GHI = 19806,
        }
    }
}
