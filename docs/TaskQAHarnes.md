# Mission: QA Harness Development and Independent Product Review

## Context

The repository has reached a state where the primary development work is considered complete and the solution appears operational.

Before promotion to the staging environment, two distinct objectives must be accomplished:

1. Develop a repository-specific QA Harness capable of supporting autonomous testing.
2. Perform an independent QA review using the harness and the Product Requirements Document (PRD).

These objectives must remain logically separated to preserve audit integrity.

---

# Phase 1 - Build the QA Harness

## Objective

Design and implement a QA Harness tailored to this repository.

The harness should encapsulate all product-specific knowledge required to test the solution effectively.

The harness is not responsible for making quality judgments.

Its purpose is to provide infrastructure and validation capabilities that can later be used by independent QA agents.

---

## Responsibilities

### Environment Provisioning

Create all required setup logic.

Examples:

* Build procedures
* Dependency installation
* Database initialization
* Configuration preparation
* Test data generation

---

### Application Startup

Provide reliable mechanisms to:

* Build the application
* Launch the application
* Verify successful startup
* Detect startup failures

---

### Workflow Library

Identify and document all known workflows.

Examples:

* User actions
* Import workflows
* Export workflows
* Administrative functions
* Configuration operations

Represent workflows as reusable automation assets.

---

### Domain Validators

Implement product-specific validators.

Examples:

* File validators
* Data validators
* Business rule validators
* Invariant validators
* Output correctness validators

Examples for engineering software:

* Coordinate validation
* Layer validation
* Parser output validation

---

### Evidence Collection

Provide APIs for:

* Screenshots
* Logs
* Videos
* Generated files
* Network traces
* Diagnostics

---

### Traceability Support

Implement structures allowing findings to be linked to:

* Requirements
* Features
* Invariants

---

### Reporting Support

Support generation of:

* Markdown reports
* HTML reports
* JSON reports

---

# Phase 1 Deliverables

Produce:

## Architecture Document

Describe:

* Components
* Extension points
* Execution model
* Data flow

---

## Harness Implementation

Working implementation integrated into the repository.

---

## Capability Inventory

Document available capabilities.

Example:

* Browser Automation
* API Testing
* File Validation
* Export Verification
* Invariant Checking

---

## Harness Limitations

Document:

* Unsupported scenarios
* Known constraints
* Areas requiring future enhancement

---

# Phase 2 - Independent QA Review

## Objective

Perform an independent evaluation of the product using:

* The Product Requirements Document (PRD)
* The running application
* Evidence collected during the current review

The review must not be influenced by historical findings.

---

# Independence Requirements

The QA review must not consume:

* Previous reports
* Issue trackers
* Historical defects
* Previous PASS/FAIL assessments
* Release notes
* Developer commentary regarding known issues

The review shall be performed as if no prior assessment exists.

---

# Authoritative Sources

The review may use only:

1. The PRD
2. The running application
3. Artifacts generated during the current execution
4. Evidence collected during the current execution

---

# Requirement Verification

Create a traceability matrix.

For every requirement:

Assign exactly one status:

* PASS
* FAIL
* NEEDS HUMAN REVIEW
* NOT TESTED

Every status must include evidence and rationale.

---

# Feature Verification

Evaluate every feature defined within the PRD.

Record:

* Feature identifier
* Feature description
* Evidence
* Status
* Justification

---

# Invariant Verification

Evaluate every invariant documented in the PRD.

Examples:

* Uniqueness constraints
* State restrictions
* Data integrity rules
* Safety rules
* Business invariants

Record:

* Invariant
* Evidence
* Status
* Justification

---

# Exploratory Testing

Perform exploratory testing beyond explicit PRD requirements.

Attempt to discover:

* Unexpected failures
* Workflow gaps
* Error handling issues
* Data loss scenarios
* Edge case failures

Document all findings.

---

# Usability Assessment

Identify observations involving:

* Navigation
* Discoverability
* Error messaging
* User effort
* Workflow clarity

When objective validation is not possible:

Assign:

NEEDS HUMAN REVIEW

and provide observations.

---

# Evidence Requirements

Every conclusion must be supported by evidence.

Acceptable evidence includes:

* Screenshots
* Logs
* Generated files
* Runtime traces
* API responses
* Measurements

Unsupported conclusions shall not be classified as PASS or FAIL.

---

# Final Report

Generate a Markdown report containing:

## Executive Summary

* Product
* Version
* Review date

---

## Requirement Summary

* Passed
* Failed
* Human Review Required
* Not Tested

---

## Feature Summary

* Passed
* Failed
* Human Review Required
* Not Tested

---

## Invariant Summary

* Passed
* Failed
* Human Review Required
* Not Tested

---

## Findings

Detailed findings with evidence.

---

## Human Review Queue

Items requiring manual assessment.

---

## Coverage Analysis

* Requirements covered
* Features covered
* Invariants covered
* Confidence level

---

## Risk Assessment

* Critical
* High
* Medium
* Low

---

## Deployment Recommendation

One of:

* READY FOR STAGING
* READY FOR STAGING WITH RISKS
* NOT READY FOR STAGING

Provide justification based solely on collected evidence.

---

# Audit Statement

Include the following statement:

"All findings in this report were produced from an independent evaluation of the current system version against the Product Requirements Document. No previous reports, issue trackers, release notes, or historical assessments were used to determine PASS, FAIL, NEEDS HUMAN REVIEW, or NOT TESTED outcomes. All conclusions are based exclusively on evidence collected during the current review."

---

# Success Criteria

The mission is successful when:

1. A reusable repository-specific QA Harness exists.
2. The harness can execute representative workflows.
3. The product has been independently evaluated against the PRD.
4. Every requirement, feature, and invariant has a documented disposition.
5. A deployment recommendation is supported by traceable evidence.
6. Human reviewers can audit every conclusion back to collected artifacts.
