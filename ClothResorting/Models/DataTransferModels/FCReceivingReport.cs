﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClothResorting.Models.DataTransferModels
{
    public class FCReceivingReport
    {
        public int Index { get; set; }

        public string PurchaseOrder { get; set; }

        public string Style { get; set; }

        public int Line { get; set; }

        public string Color { get; set; }

        public string Customer { get; set; }

        public string CartonRange { get; set; }

        public string SizeBundle { get; set; }

        public string PcsBundle { get; set; }

        public int ReceivableQty { get; set; }

        public int ReceivedQty { get; set; }

        public int ReceivableCtns { get; set; }

        public int ReceivedCtns { get; set; }

        public string SKU { get; set; }

        public string Memo { get; set; }

        public string Comment { get; set; }

        public string PreLocation { get; set; }
    }
}