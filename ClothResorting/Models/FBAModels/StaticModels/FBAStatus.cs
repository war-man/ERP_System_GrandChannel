﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClothResorting.Models.FBAModels.StaticModels
{
    public static class FBAStatus
    {
        public const string Na = "N/A";

        public const string Received = "Received";

        public const string Allocated = "Allocated";

        public const string WaitingForCharging = "Waiting for charging";

        public const string New = "New";

        public const string Returned = "Returned";

        public const string Finished = "Finished";

        public const string Pending = "Pending";

        public const string Unhandled = "Unhandled";

        public const string NoNeedForCharging = "No need for charging";

        public const string Template = "Template";

        public const string NewCreated = "New Created";

        public const string InStock = "In Stock";

        public const string Picking = "Picking";

        public const string NewOrder = "New Order";

        public const string Processing = "Processing";

        public const string Released = "Released";

        public const string Shipped = "Shipped";

        public const string InPallet = "InPallet";

        public const string Unassigned = "Unassigned";

        public const string Relocated = "Relocated";

        public const string Ready = "Ready";

        public const string LossCtn = "LossCtn";

        public const string Pallet = "Pallet";
    }
}