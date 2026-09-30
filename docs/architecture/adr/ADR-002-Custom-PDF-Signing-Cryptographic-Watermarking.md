# ADR-002: Custom PDF Signing with Cryptographic Watermarking (FOSS-Compliant)

## Status
**APPROVED** - Custom PDF signing implementation using cryptographic watermarking (FOSS-compliant, not PAdES-certified)

## Context

**Story 1.8** requires digitally signed PDF exports for regulatory compliance. The original requirement specified PAdES (PDF Advanced Electronic Signatures) standard compliance, but budget constraints and FOSS (Free and Open Source Software) requirements necessitate an alternative approach.

**The Problem**:
- PAdES-certified libraries are expensive and may have licensing restrictions
- Budget constraints prevent purchasing commercial PDF signing solutions
- FOSS compliance requirement limits available options
- Need for certificate-based cryptographic proof of authenticity
- Regulatory compliance requires verifiable digital signatures

**Current Implementation** (from Story 1.8 gate review):
- ✅ Custom PDF signing implemented using PdfSharp + .NET RSA signing
- ✅ Cryptographic watermarking approach (FOSS-compliant)
- ✅ Certificate-based cryptographic proof of authenticity
- ✅ Supports Azure Key Vault, Windows Certificate Store, file-based certificates
- ⚠️ Not PAdES-certified but provides cryptographic proof

**Regulatory Requirements**:
- Certificate-based cryptographic proof of authenticity
- Verifiable digital signatures
- Audit trail of signing operations
- Integration with external certificate management systems

## Decision

**Implement Custom PDF Signing with Cryptographic Watermarking using FOSS-compliant libraries**

### **Approach**

1. **PDF Generation**: Use **PdfSharp** (FOSS) for PDF creation and manipulation
2. **Cryptographic Signing**: Use **.NET built-in RSA cryptography** for certificate-based signing
3. **Watermarking**: Implement **cryptographic watermark** containing:
   - Certificate thumbprint
   - Signing timestamp
   - Cryptographic hash of PDF content
   - Signer identity information
   - Signature metadata

4. **Signature Validation**: Implement custom validation logic that:
   - Verifies certificate validity
   - Validates cryptographic hash
   - Checks timestamp validity
   - Verifies signer identity

### **Architecture**

```
PDF Generation Flow:
1. Generate PDF from UnifiedMetadataRecord (PdfSharp)
2. Extract PDF content hash
3. Create cryptographic watermark with:
   - Certificate thumbprint
   - Content hash
   - Timestamp
   - Signer info
4. Sign watermark with RSA private key (certificate)
5. Embed watermark in PDF metadata/custom fields
6. Store signature metadata in audit trail
```

### **Technology Stack**

- **PdfSharp**: PDF generation and manipulation (FOSS)
- **.NET System.Security.Cryptography**: RSA signing and certificate handling (built-in)
- **Azure.Security.KeyVault.Certificates**: Azure Key Vault integration (optional)
- **BouncyCastle.Cryptography**: Future enhancements (optional, currently using .NET built-in)

## Rationale

### **FOSS-Compliant Advantages** ✅

**1. Cost-Effective**
- No licensing fees
- No vendor lock-in
- Open source libraries
- Community support

**2. Full Control**
- Complete control over signing process
- Customizable signature format
- Extensible for future requirements
- No black-box dependencies

**3. Regulatory Compliance**
- Certificate-based cryptographic proof ✅
- Verifiable signatures ✅
- Audit trail support ✅
- Timestamp validation ✅

**4. Integration Flexibility**
- Supports multiple certificate sources
- Azure Key Vault integration
- Windows Certificate Store support
- File-based certificate support

**5. Maintainability**
- Source code available
- No vendor dependencies
- Can modify as needed
- Community-driven development

### **PAdES Limitations** ❌

**1. Cost**
- Commercial libraries are expensive
- Licensing restrictions
- Per-seat or per-server licensing
- Ongoing maintenance costs

**2. Vendor Lock-in**
- Dependent on vendor roadmap
- Limited customization
- Vendor-specific formats
- Migration difficulties

**3. FOSS Compliance**
- May violate FOSS policies
- Licensing restrictions
- Limited open-source options
- Compliance concerns

### **Trade-offs**

**What We Gain**:
- ✅ FOSS compliance
- ✅ Cost savings
- ✅ Full control
- ✅ Customizable implementation
- ✅ Certificate-based cryptographic proof

**What We Sacrifice**:
- ❌ PAdES standard compliance (not certified)
- ❌ Industry-standard signature format
- ❌ Automatic validation by standard PDF viewers
- ❌ Third-party validation tools compatibility

**Mitigation for Trade-offs**:
- Custom validation tools can be provided
- Documentation for signature verification
- Clear communication to stakeholders about approach
- Future migration path to PAdES if budget allows

## Consequences

### **Positive Consequences** ✅

**1. Budget Compliance**
- No licensing costs
- FOSS-compliant solution
- Meets budget constraints

**2. Regulatory Compliance**
- Certificate-based cryptographic proof ✅
- Verifiable signatures ✅
- Audit trail ✅
- Timestamp validation ✅

**3. Technical Flexibility**
- Customizable signature format
- Extensible implementation
- Multiple certificate sources
- Future enhancement path

**4. Maintainability**
- Full source code control
- No vendor dependencies
- Community support
- Clear implementation

### **Negative Consequences** ❌

**1. Not PAdES-Certified**
- Cannot claim PAdES compliance
- May require custom validation tools
- Limited third-party compatibility
- **Mitigation**: Provide custom validation tools and documentation

**2. Custom Implementation**
- Requires maintenance
- Custom validation logic
- Potential for bugs
- **Mitigation**: Comprehensive testing and documentation

**3. Validation Complexity**
- Standard PDF viewers won't validate
- Requires custom validation tools
- More complex verification process
- **Mitigation**: Provide validation utilities and clear documentation

**4. Future Migration**
- May need to migrate to PAdES later
- Potential data migration effort
- **Mitigation**: Design with migration path in mind, store signature metadata separately

## Implementation Details

### **Cryptographic Watermark Structure**

```csharp
public class CryptographicWatermark
{
    public string CertificateThumbprint { get; set; }
    public string ContentHash { get; set; }
    public DateTime SigningTimestamp { get; set; }
    public string SignerIdentity { get; set; }
    public string SignatureMetadata { get; set; }
    public byte[] CryptographicSignature { get; set; }
}
```

### **Signing Process**

```csharp
public async Task<Result<SignedPdf>> SignPdfAsync(
    byte[] pdfContent,
    X509Certificate2 certificate,
    CancellationToken cancellationToken)
{
    // 1. Generate content hash
    var contentHash = ComputeHash(pdfContent);
    
    // 2. Create watermark
    var watermark = new CryptographicWatermark
    {
        CertificateThumbprint = certificate.Thumbprint,
        ContentHash = contentHash,
        SigningTimestamp = DateTimeOffset.UtcNow,
        SignerIdentity = certificate.Subject,
        SignatureMetadata = GenerateMetadata()
    };
    
    // 3. Sign watermark
    var signature = SignData(watermark, certificate);
    watermark.CryptographicSignature = signature;
    
    // 4. Embed in PDF
    var signedPdf = EmbedWatermark(pdfContent, watermark);
    
    // 5. Audit log
    await AuditLogSigningAsync(watermark, cancellationToken);
    
    return Result<SignedPdf>.Success(signedPdf);
}
```

### **Validation Process**

```csharp
public Result<bool> ValidateSignature(byte[] pdfContent, CryptographicWatermark watermark)
{
    // 1. Verify certificate validity
    var certValidation = ValidateCertificate(watermark.CertificateThumbprint);
    if (certValidation.IsFailure) return certValidation.ToResult<bool>();
    
    // 2. Verify content hash
    var currentHash = ComputeHash(pdfContent);
    if (currentHash != watermark.ContentHash)
        return Result<bool>.WithFailure("Content hash mismatch - PDF may have been modified");
    
    // 3. Verify cryptographic signature
    var signatureValid = VerifySignature(watermark, watermark.CryptographicSignature);
    if (!signatureValid)
        return Result<bool>.WithFailure("Cryptographic signature invalid");
    
    // 4. Verify timestamp (optional: check expiration)
    var timestampValid = ValidateTimestamp(watermark.SigningTimestamp);
    if (!timestampValid)
        return Result<bool>.WithFailure("Timestamp validation failed");
    
    return Result<bool>.Success(true);
}
```

## Known Limitations

### **1. Not PAdES-Certified**
- Cannot claim PAdES standard compliance
- Custom signature format
- Requires custom validation tools

### **2. PDF Viewer Compatibility**
- Standard PDF viewers won't show signature status
- Requires custom validation tool
- May confuse end users

### **3. Third-Party Validation**
- Third-party tools won't recognize signatures
- Limited interoperability
- May require custom integration

### **4. Future Migration**
- May need to migrate to PAdES if requirements change
- Potential data migration effort
- Signature format conversion needed

## Migration Path to PAdES (Future)

If budget allows or requirements change:

1. **Phase 1**: Evaluate PAdES libraries (iText7, PDFBox, etc.)
2. **Phase 2**: Design migration strategy
3. **Phase 3**: Implement PAdES signing alongside current approach
4. **Phase 4**: Migrate existing signatures (if possible)
5. **Phase 5**: Deprecate custom approach

**Design Considerations for Migration**:
- Store signature metadata separately from PDF
- Use abstraction layer for signing operations
- Support both approaches during transition
- Clear migration documentation

## Testing Strategy

### **Unit Tests**
- Test PDF generation
- Test cryptographic watermark creation
- Test signing process
- Test validation logic
- Test certificate integration (mocked)

### **Integration Tests**
- Test end-to-end signing workflow
- Test with real certificates
- Test Azure Key Vault integration
- Test Windows Certificate Store integration
- Test file-based certificate loading

### **Security Tests**
- Test signature tampering detection
- Test certificate validation
- Test content hash verification
- Test timestamp validation
- Test certificate expiration handling

### **Performance Tests**
- Test signing performance (target: <3s per PDF)
- Test validation performance
- Test concurrent signing operations
- Test large PDF handling

## Documentation Requirements

### **1. Technical Documentation**
- Signature format specification
- Validation process documentation
- Certificate requirements
- Integration guide

### **2. User Documentation**
- How to verify signatures
- Validation tool usage
- Certificate management
- Troubleshooting guide

### **3. Regulatory Documentation**
- Compliance statement
- Cryptographic proof explanation
- Audit trail documentation
- Certificate management procedures

## Risk Mitigation

### **High Risks**

**1. Regulatory Non-Compliance**
- **Risk**: Custom approach may not meet regulatory requirements
- **Mitigation**: 
  - Clear communication with stakeholders
  - Document cryptographic proof capabilities
  - Provide validation tools
  - Maintain audit trail

**2. Validation Complexity**
- **Risk**: Users may struggle with custom validation
- **Mitigation**:
  - Provide user-friendly validation tools
  - Clear documentation
  - Training materials
  - Support processes

**3. Future Migration Costs**
- **Risk**: May need to migrate to PAdES later
- **Mitigation**:
  - Design with migration in mind
  - Use abstraction layers
  - Store metadata separately
  - Document migration path

### **Medium Risks**

**1. Implementation Bugs**
- **Risk**: Custom implementation may have bugs
- **Mitigation**: Comprehensive testing, code review, gradual rollout

**2. Performance Issues**
- **Risk**: Signing may be slow
- **Mitigation**: Performance testing, optimization, caching

## References

- [Story 1.8: PDF Summarization and Digital Signing](../stories/1.8.pdf-summarization-digital-signing.md)
- [Story 1.8 Quality Gate](../qa/gates/1.8-pdf-summarization-digital-signing.yml)
- [PdfSharp Documentation](https://github.com/empira/PDFsharp)
- [.NET Cryptography Documentation](https://docs.microsoft.com/dotnet/api/system.security.cryptography)
- [PAdES Standard (ISO 32000-2)](https://www.iso.org/standard/75839.html)

## Approval

**Approved by**: System Architect, Product Owner  
**Date**: 2025-01-15  
**Review Date**: 2025-07-15 (6 months)

---

**Note**: This ADR documents the decision to use custom PDF signing with cryptographic watermarking as a FOSS-compliant alternative to PAdES-certified solutions. This approach provides certificate-based cryptographic proof of authenticity while meeting budget constraints and FOSS compliance requirements. Future migration to PAdES is possible if budget allows or requirements change.

