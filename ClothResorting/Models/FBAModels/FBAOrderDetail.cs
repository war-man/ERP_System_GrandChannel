﻿using ClothResorting.Models.FBAModels.BaseClass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClothResorting.Models.FBAModels
{
    public class FBAOrderDetail : BaseFBAOrderDetail
    {
        public string LotSize { get; set; }

        public float GrossWeight { get; set; }

        public float CBM { get; set; }

        public string Barcode { get; set; }

        public string Symbology { get; set; }

        public int Quantity { get; set; }

        public string Remark { get; set; }

        public int ComsumedQuantity { get; set; }

        public string LabelFiles { get; set; }

        public FBAMasterOrder FBAMasterOrder { get; set; }

        public ICollection<FBACartonLocation> FBACartonLocations { get; set; }

        public FBAOrderDetail()
        {
            LotSize = string.Empty;
            HowToDeliver = string.Empty;
            GrossWeight = 0f;
            CBM = 0f;
            Quantity = 0;
            Remark = string.Empty;
            ComsumedQuantity = 0;
            LabelFiles = "[]";
        }

        public void AssembleSecontStringPart(string lotSize, string howToDeliver, string remark)
        {
            LotSize = lotSize ?? string.Empty;
            HowToDeliver = howToDeliver ?? string.Empty;
            Remark = remark ?? string.Empty;
        }

        public void AssembleNumberPart(float grossWeight, float cbm, int quantity)
        {
            GrossWeight = grossWeight;
            CBM = cbm;
            Quantity = quantity;
        }
    }

    public class LabelFile
    {
        public string OriginalName { get; set; }

        public string NameInSystem { get; set; }

        public string RootPath { get; set; }

        public DateTime UploadDate { get; set; }
    }
}