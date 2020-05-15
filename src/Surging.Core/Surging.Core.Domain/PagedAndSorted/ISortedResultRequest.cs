using System.Collections.Generic;

namespace Surging.Core.Domain.PagedAndSorted
{
    public interface ISortedResultRequest
    {
        IDictionary<string,SortType> Sorting { get; set; }

    }
}
