﻿using ClothResorting.Models;
using System;
using System.Collections.Generic;
using Microsoft.Office.Interop.Excel;
using System.Linq;
using System.Web;
using System.Data.Entity;
using ClothResorting.Models.StaticClass;
using ClothResorting.Models.FBAModels.StaticModels;
using System.Globalization;
using System.IO;
using System.Threading;
using ClothResorting.Models.FBAModels;

namespace ClothResorting.Helpers.FBAHelper
{
    public class FBAInventoryHelper
    {
        private ApplicationDbContext _context;
        private string _path = "";
        private _Application _excel;
        private Workbook _wb;
        private Worksheet _ws;
        private delegate void QuitHandler();
        //private delegate void SaveAsHandler(object Filename, object FileFormat, object Password, object WriteResPassword, object ReadOnlyRecommended, object CreateBackup, XlSaveAsAccessMode AccessMode = XlSaveAsAccessMode.xlNoChange, object ConflictResolution = null, object AddToMru = null, object TextCodepage = null, object TextVisualLayout = null, object Local = null);

        public FBAInventoryHelper()
        {
            _context = new ApplicationDbContext();
        }

        public FBAInventoryHelper(string templatePath)
        {
            _context = new ApplicationDbContext();
            _path = templatePath;
            _excel = new Application();
            _wb = _excel.Workbooks.Open(_path);
        }

        //输入截止日期和客户代码，返回到截止日期时FBA的库存列表
        public FBAInventoryInfo GetFBAInventoryResidualInfo(string customerCode, DateTime startDate, DateTime closeDate)
        {
            var residualInventoryList = new List<FBACtnInventory>();
            //在1900年与2018年之间任取一日期作为最小日期，取在下的生日
            var inboundDate = startDate;
            var rCloseDate = closeDate.AddDays(1);

            //获取在指定日期之前入库的总箱数库存列表
            var cartonLocationsInDb = _context.FBACartonLocations
                .Include(x => x.FBAPallet.FBAPalletLocations)
                .Include(x => x.FBAOrderDetail.FBAMasterOrder.Customer)
                .Include(x => x.FBAPickDetailCartons
                    .Select(c => c.FBAPickDetail.FBAShipOrder))
                .Include(x => x.FBAPickDetails
                    .Select(c => c.FBAShipOrder))
                .Where(x => x.FBAOrderDetail.FBAMasterOrder.InboundDate <= closeDate
                    && x.FBAOrderDetail.FBAMasterOrder.InboundDate >= inboundDate
                    && x.FBAOrderDetail.FBAMasterOrder.Customer.CustomerCode == customerCode);

            //获取在指定日期前入库的托盘库存列表
            var palletLocationsInDb = _context.FBAPalletLocations
                .Include(x => x.FBAMasterOrder.Customer)
                .Include(x => x.FBAPickDetails.Select(c => c.FBAShipOrder))
                .Where(x => x.FBAMasterOrder.InboundDate <= closeDate 
                    && x.FBAMasterOrder.InboundDate >= inboundDate
                    && x.FBAMasterOrder.Customer.CustomerCode == customerCode);

            var palletLocationsList = palletLocationsInDb.ToList();
            var cartonLocationList = cartonLocationsInDb.ToList();

            var originalPlts = palletLocationsList.Sum(x => x.ActualPlts);
            var originalLooseCtns = cartonLocationList.Where(x => x.Location != "Pallet").Sum(x => x.ActualQuantity);
            var currentLooseCtns = originalLooseCtns;

            var pltViewList = new List<FBAPalletGroupInventory>();

            var totalPickingPlts = 0;

            //计算原有托盘数减去所有发出的托盘数
            foreach (var plt in palletLocationsList)
            {
                var currentPickingPlt = 0;
                var currentOriginalPlts = plt.ActualPlts;

                foreach (var pick in plt.FBAPickDetails)
                {
                    if (pick.FBAShipOrder.PlaceTime < rCloseDate)
                    {
                        plt.ActualPlts -= pick.PltsFromInventory;

                        //所有运单状态不为shipped的物品都视为正在处理processing/picking
                        if (pick.FBAShipOrder.Status != FBAStatus.Shipped)
                        {
                            totalPickingPlts += pick.PltsFromInventory;
                            currentPickingPlt += pick.PltsFromInventory;
                        }
                    }
                }

                if (currentPickingPlt != 0 || plt.ActualPlts != 0)
                {
                    pltViewList.Add(new FBAPalletGroupInventory {
                        PltId = plt.Id,
                        SubCustomer = plt.FBAMasterOrder.SubCustomer,
                        Container = plt.Container,
                        AmzRefId = plt.AmzRefId,
                        ShipmentId = plt.ShipmentId,
                        ActualPlts = currentOriginalPlts,
                        PickingPlts = currentPickingPlt,
                        AvailablePlts = plt.ActualPlts,
                        Location = plt.Location,
                        PalletSize = plt.PalletSize,
                        InboundDate = plt.FBAMasterOrder.InboundDate
                    });
                }
            }

            var totalPickingCtns = 0;

            //计算原有箱数减去每次发出的箱数并放到列表中
            foreach (var cartonLocation in cartonLocationsInDb)
            {
                var originalQuantity = cartonLocation.ActualQuantity;
                var currentPickingCtns = 0;

                if (cartonLocation.Location == FBAInventoryType.Pallet)
                {
                    foreach(var pickCarton in cartonLocation.FBAPickDetailCartons)
                    {
                        //获取在指定日期前的相关拣货列表
                        if (pickCarton.FBAPickDetail.FBAShipOrder.PlaceTime < rCloseDate)
                        {
                            cartonLocation.ActualQuantity -= pickCarton.PickCtns;

                            if (pickCarton.FBAPickDetail.FBAShipOrder.Status != FBAStatus.Shipped)
                            {
                                totalPickingCtns += pickCarton.PickCtns;
                                currentPickingCtns += pickCarton.PickCtns;
                            }
                        }
                    }
                }
                else    //直接与拣货单
                {
                    foreach(var pickCarton in cartonLocation.FBAPickDetails)
                    {
                        if (pickCarton.FBAShipOrder.PlaceTime < rCloseDate)
                        {
                            cartonLocation.ActualQuantity -= pickCarton.ActualQuantity;
                            currentLooseCtns -= pickCarton.ActualQuantity;

                            if (pickCarton.FBAShipOrder.Status != FBAStatus.Shipped)
                            {
                                totalPickingCtns += pickCarton.ActualQuantity;
                                currentPickingCtns += pickCarton.ActualQuantity;
                            }
                        }
                    }
                }

                if (cartonLocation.ActualQuantity != 0 || currentPickingCtns != 0)
                {
                    var ctnInventory = new FBACtnInventory
                    {
                        SubCustomer = cartonLocation.FBAOrderDetail.FBAMasterOrder.SubCustomer,
                        Id = cartonLocation.Id,
                        Container = cartonLocation.Container,
                        Type = cartonLocation.Location == "Pallet" ? FBAStatus.InPallet : FBAStatus.LooseCtn,
                        ShipmentId = cartonLocation.ShipmentId,
                        AmzRefId = cartonLocation.AmzRefId,
                        HoldQuantity = cartonLocation.HoldCtns,
                        WarehouseCode = cartonLocation.WarehouseCode,
                        GrossWeightPerCtn = cartonLocation.GrossWeightPerCtn,
                        CBMPerCtn = cartonLocation.CBMPerCtn,
                        PickingCtns = currentPickingCtns,
                        ResidualCBM = cartonLocation.CBMPerCtn * cartonLocation.ActualQuantity,
                        ResidualQuantity = cartonLocation.ActualQuantity - cartonLocation.HoldCtns,
                        OriginalQuantity = originalQuantity,
                        InboundDate = cartonLocation.FBAOrderDetail.FBAMasterOrder.InboundDate,
                        Location = cartonLocation.Location == "Pallet" ? CombineLocation(cartonLocation.FBAPallet.FBAPalletLocations.Select(x => x.Location).ToList()) : cartonLocation.Location,
                    };

                    residualInventoryList.Add(ctnInventory);

                    if (cartonLocation.Location == FBAInventoryType.Pallet)
                    {
                        if (!cartonLocation.FBAPallet.FBAPalletLocations.Any())
                        {
                            throw new Exception("Unallocated pallets detected. Please check container: " + cartonLocation.Container + " and try again after all pallets are fully allocated");
                        }

                        var pltId = cartonLocation.FBAPallet.FBAPalletLocations.First().Id;

                        if (pltViewList.SingleOrDefault(x => x.PltId == pltId) != null)
                            pltViewList.SingleOrDefault(x => x.PltId == pltId).InPalletCtnInventories.Add(ctnInventory);
                    }
                }
            }

            var info = new FBAInventoryInfo();

            info.FBACtnInventories = residualInventoryList;
            info.FBAPalletGroupInventories = pltViewList;
            info.Customer = customerCode;

            info.OriginalTotalPlts = originalPlts;
            info.OriginalLooseCtns = originalLooseCtns;

            info.CurrentLooseCtns = currentLooseCtns;

            info.CurrentTotalPlts = palletLocationsList.Sum(x => x.ActualPlts);
            info.TotalPickingPlts = totalPickingPlts;

            info.CurrentTotalCtns = residualInventoryList.Sum(x => x.ResidualQuantity);
            info.TotalPickingCtns = totalPickingCtns;

            info.TotalInPalletCtns = (int)info.CurrentTotalCtns - info.CurrentLooseCtns;

            info.TotalResidualCBM = residualInventoryList.Sum(x => x.ResidualCBM);
            info.CloseDate = closeDate;
            info.StartDate = startDate;

            return info;
        }

        //读取库存报告模板并另存报告,且直接下载
        public void GenerateFBAInventoryReport(FBAInventoryInfo info)
        {
            _ws = _wb.Worksheets[1];

            _ws.Cells[4, 2] = info.Customer;
            _ws.Cells[4, 4] = info.CloseDate.ToString("yyyy-MM-dd");
            _ws.Cells[4, 6] = info.CurrentTotalCtns - info.CurrentLooseCtns;
            _ws.Cells[4, 8] = info.CurrentLooseCtns;

            _ws.Cells[6, 2] = info.CurrentTotalPlts;
            _ws.Cells[6, 4] = info.TotalPickingPlts;
            _ws.Cells[6, 6] = info.CurrentTotalCtns;
            _ws.Cells[6, 8] = info.TotalPickingCtns;

            var startRow = 9;

            foreach(var i in info.FBACtnInventories)
            {
                _ws.Cells[startRow, 1] = i.Container;
                _ws.Cells[startRow, 2] = i.SubCustomer;
                _ws.Cells[startRow, 3] = i.Type;
                _ws.Cells[startRow, 4] = i.ShipmentId;
                _ws.Cells[startRow, 5] = i.AmzRefId;
                _ws.Cells[startRow, 6] = i.WarehouseCode;
                _ws.Cells[startRow, 7] = Math.Round(i.GrossWeightPerCtn, 2);
                _ws.Cells[startRow, 8] = Math.Round(i.CBMPerCtn, 2);
                _ws.Cells[startRow, 9] = i.OriginalQuantity;
                _ws.Cells[startRow, 10] = i.PickingCtns;
                _ws.Cells[startRow, 11] = Math.Round((double)i.ResidualQuantity, 2);
                _ws.Cells[startRow, 12] = Math.Round((double)i.HoldQuantity, 2);
                _ws.Cells[startRow, 13] = i.Location;

                startRow += 1;
            }

            _ws.get_Range("A1:M" + startRow, Type.Missing).HorizontalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:M" + startRow, Type.Missing).VerticalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:M" + startRow, Type.Missing).Borders.LineStyle = 1;

            _ws = _wb.Worksheets[2];

            _ws.Cells[4, 2] = info.Customer;
            _ws.Cells[4, 4] = info.CloseDate.ToString("yyyy-MM-dd");
            _ws.Cells[4, 6] = info.CurrentTotalCtns - info.CurrentLooseCtns;
            _ws.Cells[4, 8] = info.CurrentLooseCtns;

            _ws.Cells[6, 2] = info.CurrentTotalPlts;
            _ws.Cells[6, 4] = info.TotalPickingPlts;
            _ws.Cells[6, 6] = info.CurrentTotalCtns;
            _ws.Cells[6, 8] = info.TotalPickingCtns;

            startRow = 9;

            foreach(var g in info.FBAPalletGroupInventories)
            {
                var ctnIndex = startRow;

                _ws.Cells[startRow, 1] = g.PltId;
                _ws.Cells[startRow, 2] = g.Container;
                _ws.Cells[startRow, 3] = g.SubCustomer;
                _ws.Cells[startRow, 4] = g.ActualPlts;
                _ws.Cells[startRow, 5] = g.PickingPlts;
                _ws.Cells[startRow, 6] = g.AvailablePlts;
                _ws.Cells[startRow, 7] = g.Location;

                foreach(var c in g.InPalletCtnInventories)
                {
                    _ws.Cells[ctnIndex, 8] = c.Id;
                    _ws.Cells[ctnIndex, 9] = c.ShipmentId;
                    _ws.Cells[ctnIndex, 10] = c.AmzRefId;
                    _ws.Cells[ctnIndex, 11] = c.WarehouseCode;
                    _ws.Cells[ctnIndex, 12] = c.GrossWeightPerCtn;
                    _ws.Cells[ctnIndex, 13] = c.CBMPerCtn;
                    _ws.Cells[ctnIndex, 14] = c.OriginalQuantity;
                    _ws.Cells[ctnIndex, 15] = c.PickingCtns;
                    _ws.Cells[ctnIndex, 16] = c.ResidualQuantity;
                    _ws.Cells[ctnIndex, 17] = c.HoldQuantity;

                    ctnIndex += 1;
                }

                //如果一托盘里面有很多SKU，则合并托盘单元格
                if (g.InPalletCtnInventories.Count > 1)
                {
                    var rangeId = _ws.get_Range("A" + startRow, "A" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeId.Merge(rangeId.MergeCells);

                    var rangeContainer = _ws.get_Range("B" + startRow, "B" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeContainer.Merge(rangeContainer.MergeCells);

                    var rangeSunCustomer = _ws.get_Range("C" + startRow, "C" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeSunCustomer.Merge(rangeSunCustomer.MergeCells);

                    var rangeOrgPlt = _ws.get_Range("D" + startRow, "D" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeOrgPlt.Merge(rangeOrgPlt.MergeCells);

                    var rangePlt = _ws.get_Range("E" + startRow, "E" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangePlt.Merge(rangePlt.MergeCells);

                    var rangeStockPlt = _ws.get_Range("F" + startRow, "F" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeStockPlt.Merge(rangeStockPlt.MergeCells);

                    var rangeLocation = _ws.get_Range("G" + startRow, "G" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeLocation.Merge(rangeLocation.MergeCells);
                }

                startRow += g.InPalletCtnInventories.Count;
            }

            _ws.get_Range("A1:O" + startRow, Type.Missing).HorizontalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:O" + startRow, Type.Missing).VerticalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:O" + startRow, Type.Missing).Borders.LineStyle = 1;

            var fullPath = @"D:\InventoryReport\FBA-" + info.Customer + "-InventoryReport-" + DateTime.Now.ToString("yyyyMMddhhmmssffff") + ".xls";

            _wb.SaveAs(fullPath, Type.Missing, "", "", Type.Missing, Type.Missing, XlSaveAsAccessMode.xlNoChange, 1, false, Type.Missing, Type.Missing, Type.Missing);

            _excel.Quit();

            //使用主线程调用委托，等于用主线程调用Quick()方法，以达到阻塞线程的目的
            var handler = new QuitHandler(_excel.Quit);
            handler.Invoke();

            var response = HttpContext.Current.Response;
            var downloadFile = new FileInfo(fullPath);
            response.ClearHeaders();
            response.Buffer = false;
            response.ContentType = "application/octet-stream";
            response.AppendHeader("Access-Control-Allow-Origin", "*");
            response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE");
            response.AppendHeader("Access-Control-Allow-Headers", "Content-Type");
            response.AppendHeader("Content-Disposition", "attachment; filename=" + info.Customer + "-Inventory Report-" + HttpUtility.UrlEncode(DateTime.Now.ToString("yyyyMMddhhmmss") + ".xls", System.Text.Encoding.UTF8));
            response.Clear();
            response.AppendHeader("Content-Length", downloadFile.Length.ToString());
            response.WriteFile(downloadFile.FullName);
            response.Flush();
            response.Close();
            response.End();

            var killer = new ExcelKiller();

            killer.Dispose();
        }

        public string GenerateAndReturnFBAInventoryReportPath(FBAInventoryInfo info)
        {
            _ws = _wb.Worksheets[1];

            _ws.Cells[4, 2] = info.Customer;
            _ws.Cells[4, 4] = DateTime.Now.ToString("yyyy-MM-dd");
            _ws.Cells[4, 6] = info.CurrentTotalCtns - info.CurrentLooseCtns;
            _ws.Cells[4, 8] = info.CurrentLooseCtns;

            _ws.Cells[4, 10] = info.StartDate.ToString("yyyy-MM-dd");
            _ws.Cells[4, 12] = info.CloseDate.ToString("yyyy-MM-dd");

            _ws.Cells[6, 2] = info.CurrentTotalPlts;
            _ws.Cells[6, 4] = info.TotalPickingPlts;
            _ws.Cells[6, 6] = info.CurrentTotalCtns;
            _ws.Cells[6, 8] = info.TotalPickingCtns;

            var startRow = 9;

            foreach (var i in info.FBACtnInventories)
            {
                _ws.Cells[startRow, 1] = i.Container;
                _ws.Cells[startRow, 2] = i.SubCustomer;
                _ws.Cells[startRow, 3] = i.Type;
                _ws.Cells[startRow, 4] = i.ShipmentId;
                _ws.Cells[startRow, 5] = i.AmzRefId;
                _ws.Cells[startRow, 6] = i.WarehouseCode;
                _ws.Cells[startRow, 7] = Math.Round(i.GrossWeightPerCtn, 2);
                _ws.Cells[startRow, 8] = Math.Round(i.CBMPerCtn, 2);
                _ws.Cells[startRow, 9] = i.OriginalQuantity;
                _ws.Cells[startRow, 10] = i.PickingCtns;
                _ws.Cells[startRow, 11] = Math.Round((double)i.ResidualQuantity, 2);
                _ws.Cells[startRow, 12] = Math.Round((double)i.HoldQuantity, 2);
                _ws.Cells[startRow, 13] = i.Location;
                _ws.Cells[startRow, 14] = i.InboundDate.ToString("yyyy-MM-dd");

                startRow += 1;
            }

            _ws.get_Range("A1:N" + startRow, Type.Missing).HorizontalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:N" + startRow, Type.Missing).VerticalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:N" + startRow, Type.Missing).Borders.LineStyle = 1;

            _ws = _wb.Worksheets[2];

            _ws.Cells[4, 2] = info.Customer;
            _ws.Cells[4, 4] = DateTime.Now.ToString("yyyy-MM-dd");
            _ws.Cells[4, 6] = info.CurrentTotalCtns - info.CurrentLooseCtns;
            _ws.Cells[4, 8] = info.CurrentLooseCtns;

            _ws.Cells[4, 10] = info.StartDate.ToString("yyyy-MM-dd");
            _ws.Cells[4, 12] = info.CloseDate.ToString("yyyy-MM-dd");

            _ws.Cells[6, 2] = info.CurrentTotalPlts;
            _ws.Cells[6, 4] = info.TotalPickingPlts;
            _ws.Cells[6, 6] = info.CurrentTotalCtns;
            _ws.Cells[6, 8] = info.TotalPickingCtns;

            startRow = 9;

            foreach (var g in info.FBAPalletGroupInventories)
            {
                var ctnIndex = startRow;

                _ws.Cells[startRow, 1] = g.PltId;
                _ws.Cells[startRow, 2] = g.Container;
                _ws.Cells[startRow, 3] = g.SubCustomer;
                _ws.Cells[startRow, 4] = g.ActualPlts;
                _ws.Cells[startRow, 5] = g.PickingPlts;
                _ws.Cells[startRow, 6] = g.AvailablePlts;
                _ws.Cells[startRow, 7] = g.PalletSize;
                _ws.Cells[startRow, 8] = g.Location;

                foreach (var c in g.InPalletCtnInventories)
                {
                    _ws.Cells[ctnIndex, 9] = c.Id;
                    _ws.Cells[ctnIndex, 10] = c.ShipmentId;
                    _ws.Cells[ctnIndex, 11] = c.AmzRefId;
                    _ws.Cells[ctnIndex, 12] = c.WarehouseCode;
                    _ws.Cells[ctnIndex, 13] = Math.Round(c.GrossWeightPerCtn, 2);
                    _ws.Cells[ctnIndex, 14] = Math.Round(c.CBMPerCtn, 2);
                    _ws.Cells[ctnIndex, 15] = c.OriginalQuantity;
                    _ws.Cells[ctnIndex, 16] = c.PickingCtns;
                    _ws.Cells[ctnIndex, 17] = c.ResidualQuantity;
                    _ws.Cells[ctnIndex, 18] = c.HoldQuantity;
                    _ws.Cells[startRow, 19] = c.InboundDate.ToString("yyyy-MM-dd");

                    ctnIndex += 1;
                }

                //如果一托盘里面有很多SKU，则合并托盘单元格
                if (g.InPalletCtnInventories.Count > 1)
                {
                    var rangeId = _ws.get_Range("A" + startRow, "A" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeId.Merge(rangeId.MergeCells);

                    var rangeContainer = _ws.get_Range("B" + startRow, "B" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeContainer.Merge(rangeContainer.MergeCells);

                    var rangeSunCustomer = _ws.get_Range("C" + startRow, "C" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeSunCustomer.Merge(rangeSunCustomer.MergeCells);

                    var rangeOrgPlt = _ws.get_Range("D" + startRow, "D" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeOrgPlt.Merge(rangeOrgPlt.MergeCells);

                    var rangePlt = _ws.get_Range("E" + startRow, "E" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangePlt.Merge(rangePlt.MergeCells);

                    var rangeStockPlt = _ws.get_Range("F" + startRow, "F" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeStockPlt.Merge(rangeStockPlt.MergeCells);

                    var rangePltSize = _ws.get_Range("G" + startRow, "G" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangePltSize.Merge(rangePltSize.MergeCells);

                    var rangeLocation = _ws.get_Range("H" + startRow, "H" + (startRow + g.InPalletCtnInventories.Count - 1));
                    rangeLocation.Merge(rangeLocation.MergeCells);
                }

                startRow += g.InPalletCtnInventories.Count;
            }

            _ws.get_Range("A1:S" + startRow, Type.Missing).HorizontalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:S" + startRow, Type.Missing).VerticalAlignment = XlVAlign.xlVAlignCenter;
            _ws.get_Range("A1:S" + startRow, Type.Missing).Borders.LineStyle = 1;

            var fullPath = @"D:\InventoryReport\FBA-" + info.Customer + "-InventoryReport-" + DateTime.Now.ToString("yyyyMMddhhmmssffff") + ".xls";

            _wb.SaveAs(fullPath, Type.Missing, "", "", Type.Missing, Type.Missing, XlSaveAsAccessMode.xlNoChange, 1, false, Type.Missing, Type.Missing, Type.Missing);

            _excel.Quit();

            return fullPath;
        }

        //返回库存CBM不为0的FBA客户列表
        public IList<FBAInventoryInfo> ReturnNonZeroCBMInventoryInfo(DateTime startDate, DateTime closeDate)
        {
            //DateTime closeDateInDateTime;
            //DateTime.TryParseExact(closeDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out closeDateInDateTime);

            var customerCodeList = _context.UpperVendors.Where(x => x.DepartmentCode == "FBA").Select(x => x.CustomerCode).ToList();
            var resultList = new List<FBAInventoryInfo>();

            foreach(var code in customerCodeList)
            {
                var info = GetFBAInventoryResidualInfo(code, startDate, closeDate);
                if (info.FBACtnInventories.Count != 0)
                {
                    resultList.Add(info);
                }
            }

            return resultList;
        }

        public FBAInventoryInfo ReturnInventoryInfoByCustomerCode(string customerCode, DateTime startDate, DateTime closeDate)
        {
            var info = GetFBAInventoryResidualInfo(customerCode, startDate, closeDate);

            return info;
        }

        public string CombineLocation(IList<string> locationList)
        {
            if (locationList.Count != 0)
            {
                var result = locationList.First();

                for (int i = 1; i < locationList.Count; i++)
                {
                    result = result + "/" + locationList[i];
                }

                return result;
            }
            else
            {
                return "未分配库位";
            }

        }
    }

    public class FBACtnInventory
    {
        public int Id { get; set; }

        public string Container { get; set; }

        public string Type { get; set; }

        public string ShipmentId { get; set; }

        public string AmzRefId { get; set; }

        public string WarehouseCode { get; set; }

        public float GrossWeightPerCtn { get; set; }

        public float CBMPerCtn { get; set; }

        public int PickingCtns { get; set; }

        public int OriginalQuantity { get; set; }

        public float ResidualCBM { get; set; }

        public int ResidualQuantity { get; set; }

        public int HoldQuantity { get; set; }

        public string Location { get; set; }

        public string SubCustomer { get; set; }

        public DateTime InboundDate { get; set; }
    }

    public class FBAInventoryInfo
    {
        public string Customer { get; set; }

        public int OriginalTotalPlts { get; set; }

        public int CurrentTotalPlts { get; set; }

        public float CurrentTotalCtns { get; set; }

        public int OriginalLooseCtns { get; set; }

        public int CurrentLooseCtns { get; set; }

        public float TotalResidualCBM { get; set; }

        public int TotalPickingPlts { get; set; }

        public int TotalPickingCtns { get; set; }

        public DateTime CloseDate { get; set; }

        public DateTime StartDate { get; set; }

        public int TotalInPalletCtns { get; set; }

        public List<FBACtnInventory> FBACtnInventories { get; set; }

        public List<FBAPalletGroupInventory> FBAPalletGroupInventories { get; set; }
    }

    public class FBAPalletGroupInventory
    {
        public int PltId { get; set; }

        public string SubCustomer { get; set; }

        public string Container { get; set; }

        public string Location { get; set; }

        public int ActualPlts { get; set; }

        public string ShipmentId { get; set; }

        public string AmzRefId { get; set; }

        public int PickingPlts { get; set; }

        public int AvailablePlts { get; set; }

        public string PalletSize { get; set; }

        public DateTime InboundDate { get; set; }

        public List<FBACtnInventory> InPalletCtnInventories { get; set; }

        public FBAPalletGroupInventory()
        {
            InPalletCtnInventories = new List<FBACtnInventory>();
        }
    }
}