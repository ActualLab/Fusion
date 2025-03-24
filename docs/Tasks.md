Code:
- .Require(...): move to ActualLab.Require assembly?

"AOT friendliness" refactoring:
- Result<T> - create helper allowing to use object instead (object or ErrorResult)
- Computed<T> -> Computed, State<T> -> State, etc.
- Minimize the number of T-dependent methods in Computed<T>, State<T>, etc.
- Maybe do the same in RPC
- Symbol: -> ref struct or remove, avoid using it in public API?
- ISymbolIdentifier descendants: convert to classes? 

Docs:
- Rewrite documentation
