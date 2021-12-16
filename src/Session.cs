using PSOpenAD.Native;
using System;

namespace PSOpenAD
{
    public sealed class OpenADSession
    {
        public Uri Uri { get; }

        public AuthenticationMethod Authentication { get; }

        public bool IsSigned { get; }

        public bool IsEncrypted { get; }

        public bool IsClosed { get; internal set; } = false;

        internal SafeLdapHandle Handle { get; }

        internal OpenADSession(SafeLdapHandle ldap, Uri uri, AuthenticationMethod auth, bool isSigned, bool isEncrypted)
        {
            Handle = ldap;
            Uri = uri;
            Authentication = auth;
            IsSigned = isSigned;
            IsEncrypted = isEncrypted;
        }
    }
}
