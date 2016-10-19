using System.Net.Http;
using System.IO;

namespace Slamby.Common.SlambyConnection
{
    public class SlambyStreamContent : StreamContent
    {
        public SlambyStreamContent(Stream content) : base(content)
        {
        }

        protected override void Dispose(bool disposing)
        {
            //do nothing
        }

        public void DisposeManually(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    public class SlambyByteArrayContent : ByteArrayContent
    {
        public SlambyByteArrayContent(byte[] content) : base(content) { }


        protected override void Dispose(bool disposing)
        {
            //do nothing
        }

        public void DisposeManually(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}