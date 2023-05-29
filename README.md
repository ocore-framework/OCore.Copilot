# OCore.Copilot

A ChatGPT based copilot for OCore.

It currently sort of hard-codes a greenfield-process from bird's eye perspective
and goes on to interactively fine tune the steps, identifying actors and concepts,
use-cases and tasks, and so on.

It does this by splitting up the process into steps and personas that have different
roles in the process. 

The personas are currently:

- Business Person/Stakeholder
- Team lead
- Developer

These personas have separate preambles, and will be exposed to select concepts that
are pushed through the process.

For example, the Stakeholder doesn't know too much about the technical details, but the team
lead knows everything from business-case to general development.

The developer is exposed to the technical details, but not the business case, etc.

These exposures are currently haphazard.

The generated code resembles compiling code, but is currently not.

Also, the actual code is naïve at best, and is currently not even close to being something
that should be pushed on the public, but hey, here we are. :)