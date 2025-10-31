using System;
using System.Collections.Generic;

namespace CarTechAssist.Contracts.Common
{
    public record PagedResult<T>(
        IReadOnlyList<T> Items,
        int Total,
        int Page,
        int PageSize
    );
}


