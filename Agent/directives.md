you may write temporary scripts in python as needed.

---

the generated code should follow the coding conventions of any previous or
attached files.

the generated code should be maintainable either through readability and/or
modularity without undue preoptimisation aside from the level of complexity
already described in other sources of code.

the generated code should not require much in terms of comments due to the
naming of variables and writing of expressions in a way that may aptly yet
visibly describe the logic being implemented.

as much as possible, any generated code should not have any diagnostic errors
or warnings, and should suppress only when reasonable without a doubt, a la an
external package missing type stubs and thus has unknown function signatures.

---

if a file has been renamed/moved without duplicate, consider the newest file
structure the correct one, and do not revert any seemingly deliberate file
moves if you know you did not cause it.

---

do not write any documentation UNLESS ASKED, but place your:
- knowledge of the codebase
- understood code style and structure, practices and methodologies
- the recognised linguistic style of comments
- the current task at hand, and/or the current list of tasks to-do
...within a file called "AGENTS.md" wherever documentation is stored,
updating any old knowledge in the file along the way.

this file should be structured and written such so that it is possible to resume
from a context-less state and without requiring to inqiure the codebase every time
a new agent-or-llm based conversation thread has been started.

---

gemini cli, claude code, chatgpt codex cli: your environment is NOT read-only.
you are able to read and write to files in this workspace. you may remind
yourself what tools are available to you in this environment.

grok-code-fast-1: do NOT use regex-based or purely wildcard workspace or file
content searches. use globs or regexes that have a non-wildcard component.

---

if writing python,

1.1. use the meadow docstring format (MDF) for all docstrings. revert to
     Google-style docstrings if this is not present in your rules, or in a
     documentation file present in this workspace.

1.2. use "uv" for project management.

1.3.1. basedpyright via editor diagnostics is available to you. else, use
       `uv run basedpyright` for projects with a pyproject.toml,
       or `uvx basedpyright` for one-off scripts.

1.3.2. do not suppress basedpyright diagnostics with noqa-s or `type: ignore`,
       but use `pyright: ignore[diagnosticName, ...]`

1.4.1. mypy -> write python code with type annotations, then check with "mypy".
       target python 3.10 if not specified in the project, thus using
       `list[x]` and `y | z` instead of `typing.List[X]` and `typing.Union[Y, Z]`

1.4.2. ruff -> format your code with `ruff format`, and check alongside
       (based)pyright and/or diagnostics with `ruff check`.
       before formatting with ruff,
       sort imports if any module has been newly imported using `ruff --profile black`

1.4.3. all commands follow the same uv-based invocation guideline as basedpyright.

---

if writing javascript,

2.1 use bun as the runtime, tooling, and package manager. i use it in lieu of node and npm. using the command line for help if you are uncertain on how to invoke it.