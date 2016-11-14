using Microsoft.AspNetCore.DataProtection;
using Slamby.Common.Services.Interfaces;

namespace Slamby.Common.Services
{
    public class SecureStoreDecorator : IStore
    {
        IDataProtector protector { get; set; }

        readonly IStore store;

        public SecureStoreDecorator(IStore store, IDataProtector protector)
        {
            this.store = store;
            this.protector = protector;
        }

        public bool Exists() => store.Exists();

        public bool HasContent() => store.HasContent();

        public string Read()
        {
            if (!Exists())
            {
                return string.Empty;
            }

            var content = store.Read();

            if (!string.IsNullOrWhiteSpace(content))
            {
                content = protector.Unprotect(content);
            }

            return content;
        }

        public void Write(string content)
        {
            var encodedContent = protector.Protect(content);

            store.Write(encodedContent);
        }
    }
}
