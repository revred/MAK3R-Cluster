# MAK3R Digital Twin Work Directory

This directory contains work-in-progress implementations for the PLCO-Twin (Progressive, Live, Chatty & Opinionated Digital Twin) system.

## Structure

```
work/
├── README.md                    # This file
├── ROLES.md                     # Team roles and responsibilities
├── planning/                    # Implementation planning docs
├── prototypes/                  # Experimental code and proofs-of-concept  
├── datasets/                    # Sample and test datasets
└── reports/                     # Progress reports and analysis
```

## Current Focus

**Phase P0: Enablement & Guardrails** - Foundation setup
- Infrastructure and development tools
- Security and compliance setup
- Basic data structures and patterns

**Phase P1: Digital Spine & Evidence (KG)** - Core knowledge graph
- Entity-Attribute-Value (EAV) model
- Evidence tracking with lineage
- Event ledger (append-only)

## Key Metrics (MVP Targets)

- **TTAI**: Time-to-Actionable Insight ≤ 60 minutes
- **IG@10**: Information Gain at 10 questions ≥ 60%
- **QBR**: Question Budget Ratio ≤ 0.4
- **ECR**: Evidence Coverage Ratio ≥ 95%
- **SPOF-Recall**: Single Point of Failure detection ≥ 0.9

## Getting Started

1. Review the Digital Twin work package: `docs/DIGITAL_TWIN_Work_Package_v2.md`
2. Check current implementation status in todo tracking
3. Run development environment: `make run-api` and `make run-pwa`
4. Review test scenarios: `tests/datasets/v0.1/`

## Development Principles

- **Evidence-First**: Every insight must have traceable evidence
- **Privacy-Safe**: ACL-aware routing, cross-BU aggregates only
- **Non-Destructive**: Version facts, never overwrite
- **Question-Efficient**: Maximize information gain per question
- **Grounded Insights**: Link all conclusions to source documents/events