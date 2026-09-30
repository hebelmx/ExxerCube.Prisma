# Security Vulnerability Suppressions

This document tracks security vulnerability warnings that have been suppressed in this project, along with the rationale for each suppression.

## Suppressed Vulnerabilities

### NU1902 - OpenTelemetry.Api Vulnerability

**Package:** `OpenTelemetry.Api` 1.10.0  
**Vulnerability ID:** GHSA-8785-wc3w-h8q6  
**Severity:** Moderate  
**Status:** Suppressed (documented)

**Rationale:**
- This is a transitive dependency from `OpenTelemetry.Instrumentation.Process` (1.10.0-beta.1)
- We do not have direct control over this dependency version
- The OpenTelemetry team is aware of the issue and will address it in future package versions
- The vulnerability is moderate severity, not critical
- This is a known issue in the OpenTelemetry ecosystem that affects many projects

**Risk Assessment:**
- **Impact:** Moderate - affects telemetry data collection, not core application functionality
- **Exploitability:** Low - requires specific conditions to exploit
- **Mitigation:** Monitor OpenTelemetry package updates and upgrade when fixes are available

**Monitoring:**
- Check OpenTelemetry release notes regularly
- Update packages when vulnerability is fixed
- Review: https://github.com/advisories/GHSA-8785-wc3w-h8q6

**Last Reviewed:** 2025-01-15  
**Next Review:** When OpenTelemetry packages are updated

---

### NU1903 - DocumentFormat.OpenXml Vulnerability

**Package:** `DocumentFormat.OpenXml` 3.3.0  
**Status:** Suppressed (documented)

**Rationale:**
- Required for DOCX file processing functionality
- Vulnerability is in a dependency we don't directly control
- No alternative package available that provides the same functionality
- Risk is mitigated by input validation and file type checking

**Last Reviewed:** 2025-01-15

---

## Suppression Policy

1. **Documentation Required:** All suppressed vulnerabilities must be documented here
2. **Regular Review:** Suppressed vulnerabilities should be reviewed quarterly
3. **Upgrade Priority:** When fixes become available, prioritize upgrading affected packages
4. **Risk Assessment:** Document the risk level and mitigation strategies
5. **Approval:** Security suppressions require documented rationale

## Review Schedule

- **Quarterly Review:** Check for updates to suppressed packages
- **After Security Advisories:** Review when new advisories are published
- **Before Production Deployments:** Verify suppression status

