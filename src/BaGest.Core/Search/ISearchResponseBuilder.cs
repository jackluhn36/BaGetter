using System.Collections.Generic;
using BaGest.Protocol.Models;

namespace BaGest.Core;

public interface ISearchResponseBuilder
{
    public SearchResponse BuildSearch(IReadOnlyList<PackageRegistration> results);
    public AutocompleteResponse BuildAutocomplete(IReadOnlyList<string> data);
    public DependentsResponse BuildDependents(IReadOnlyList<PackageDependent> results);
}
