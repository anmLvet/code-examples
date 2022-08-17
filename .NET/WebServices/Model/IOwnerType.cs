using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DEVCOMP.APP.SRV.WebServices.Model
{
    interface IOwnerType
    {
        Nullable<long> SRV_OWNERTYPEID { get; set; }
        SRV_OWNERTYPE SRV_OWNERTYPE { get; set; }
    }
}
