[private]
just:
    just -l

alias f := format
alias ff := format-full
alias vf := verify-format
alias vff := verify-format-full

format:
	dotnet format whitespace TombCompiler.sln --no-restore

format-full:
	dotnet format TombCompiler.sln --no-restore

verify-format:
	dotnet format whitespace TombCompiler.sln --no-restore --verify-no-changes

verify-format-full:
	dotnet format TombCompiler.sln --no-restore --verify-no-changes
