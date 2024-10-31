using PSOpenAD.LDAP;
using System;
using System.Formats.Asn1;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace PSOpenAD;

internal class PipelineLDAPSession : LDAPSession
{
    private readonly Pipe _outgoing = new();

    public PipeReader Outgoing => _outgoing.Reader;

    public PipelineLDAPSession(int version = 3, StreamWriter? writer = null) : base(version, writer)
    {}

    public override async Task CloseConnectionAsync()
    {
        await _outgoing.Writer.CompleteAsync();
        await _outgoing.Writer.FlushAsync();
    }

    public override async Task WriteDataAsync(AsnWriter writer)
    {
        Memory<byte> buffer = _outgoing.Writer.GetMemory(writer.GetEncodedLength());
        TraceMsg("SEND", buffer.Span);
        int written = writer.Encode(buffer.Span);
        _outgoing.Writer.Advance(written);
        await _outgoing.Writer.FlushAsync();
    }
}
