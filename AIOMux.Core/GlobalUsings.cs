// Disambiguate AIOMux.Core.Models.ExecutionContext from System.Threading.ExecutionContext,
// which is pulled in by the SDK's implicit global usings for System.Threading.
global using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

global using AIOMux.Core.Builders;
