using System;
using System.Linq;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Interfaces;
using NetSparkleUpdater.SignatureVerifiers;

namespace ACEOptimizer.Services
{
    internal sealed class TrustedUpdateSignatureVerifier : ISignatureVerifier
    {
        private readonly Ed25519Checker[] _verifiers;
        private SecurityMode _securityMode;

        public TrustedUpdateSignatureVerifier(params string[] publicKeys)
        {
            _securityMode = SecurityMode.Strict;
            _verifiers = publicKeys
                .Where(publicKey => !string.IsNullOrWhiteSpace(publicKey))
                .Select(publicKey => new Ed25519Checker(
                    SecurityMode.Strict,
                    publicKey,
                    publicKeyFile: null,
                    readFileBeingVerifiedInChunks: true))
                .ToArray();
        }

        public SecurityMode SecurityMode
        {
            get => _securityMode;
            set
            {
                _securityMode = value;
                foreach (Ed25519Checker verifier in _verifiers)
                    verifier.SecurityMode = value;
            }
        }

        public bool HasValidKeyInformation()
        {
            return _verifiers.Any(verifier => verifier.HasValidKeyInformation());
        }

        public ValidationResult VerifySignature(string signature, byte[] dataToVerify)
        {
            return Verify(verifier => verifier.VerifySignature(signature, dataToVerify));
        }

        public ValidationResult VerifySignatureOfFile(string signature, string binaryPath)
        {
            return Verify(verifier => verifier.VerifySignatureOfFile(signature, binaryPath));
        }

        public ValidationResult VerifySignatureOfString(string signature, string data)
        {
            return Verify(verifier => verifier.VerifySignatureOfString(signature, data));
        }

        private ValidationResult Verify(Func<Ed25519Checker, ValidationResult> verification)
        {
            foreach (Ed25519Checker verifier in _verifiers)
            {
                try
                {
                    if (verification(verifier) == ValidationResult.Valid)
                        return ValidationResult.Valid;
                }
                catch
                {
                }
            }

            return ValidationResult.Invalid;
        }
    }
}
