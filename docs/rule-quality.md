# Rule Quality Principles

DotDet rules should help an experienced .NET developer decide what to inspect and
fix next. Rule count is not a success metric. The preferred outcome is a smaller
set of well-supported, contextual, and actionable findings.

## Evidence first

Every production-impacting finding should explain:

- what DotDet observed;
- where it was observed;
- what expected condition was absent or violated;
- why the condition matters in production;
- what implementation pattern is recommended;
- how confident the engine is and how it detected the issue.

Project, file, line, rule ID, confidence, and detection method should be present
when the source supports them. If exact location is unavailable, the explanation
must say so rather than imply precision.

## Context-aware applicability

A rule must decide whether it applies before evaluating failure conditions.
Examples:

- API rules apply only when API intent is supported by host/project evidence.
- Web UI projects are not required to expose Swagger solely because they host
  ASP.NET Core.
- EF rules apply only where EF Core is actually used.
- test projects do not affect production-readiness scoring by default.
- architecture rules require enough project/layer context to establish a
  meaningful boundary.
- DI rules exclude framework-provided services and recognize common composition
  methods where evidence exists.

Unknown context should reduce confidence or suppress the production-impacting
finding, not create an Error by default.

## Low-noise design

- Group repeated evidence under one root cause when the remediation is shared.
- Avoid reporting the same configuration omission once per dependent class.
- Keep informational observations visible only when they teach something useful.
- Do not let Info or Low-confidence findings dominate score, top risks, or roadmap.
- Prefer one actionable finding with multiple evidence points over many nearly
  identical rows.

## Severity guidance

- **Critical** - confirmed release blocker with immediate, material production
  impact. Use rarely.
- **Error** - high-confidence defect likely to affect production safety or
  reliability and requiring correction or explicit acceptance.
- **Warning** - credible risk or missing readiness practice that needs review but
  is not proven to block release.
- **Info** - non-blocking observation, uncertain applicability, or educational
  guidance.

Severity must reflect both impact and evidence. Package presence alone, naming
alone, or absence without applicability evidence rarely justifies Error.

## Confidence guidance

- **High** - semantic symbol evidence, explicit project reference/configuration,
  or an unambiguous invocation/attribute.
- **Medium** - strong syntax or usage-pattern inference with plausible alternate
  explanations.
- **Low** - heuristic, incomplete loading, or ambiguous project intent.

High confidence can support Error. Medium confidence should normally remain a
Warning. Low confidence should normally be Info or excluded from production
scoring.

## Suppressions and dispositions

Users can classify a finding as Ignore, False Positive, or Accepted Risk. These
states should preserve the original evidence for auditability while removing the
finding from active production risk. Suppressions should include a reason and,
where useful, an expiration date.

- **Ignore** - intentionally hidden from active work without claiming the rule is
  incorrect.
- **False Positive** - the rule does not apply or the detection is wrong.
- **Accepted Risk** - the finding is valid but the team consciously accepts it.

Disposition is not a substitute for improving a noisy rule. Repeated false-
positive patterns should become analyzer regression fixtures.

## Reporting quality problems

When reporting a false positive, include a minimal representative project or code
fixture, rule ID, detected evidence, expected behavior, project intent, and
analysis fidelity. Remove proprietary source and secrets.

For a false negative, describe the production risk DotDet missed and provide a
safe minimal example showing the expected evidence. Security-sensitive reports
should follow [responsible disclosure](security.md#responsible-disclosure).

## Evaluating a new rule

A proposed rule should answer:

1. What concrete production risk does it identify?
2. What project contexts make it applicable or inapplicable?
3. What evidence supports each severity and confidence level?
4. Can framework behavior or composition methods explain the condition?
5. How will repeated evidence be grouped?
6. What is the recommended remediation and official documentation?
7. Which positive, negative, mixed-host, test-project, and degraded-loading
   fixtures cover it?
8. How does it affect category scoring and top-risk ranking?

Do not enable a rule by default until representative repositories show that its
signal is useful and its false-positive rate is acceptable.
