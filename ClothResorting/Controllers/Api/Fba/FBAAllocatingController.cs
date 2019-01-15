﻿using AutoMapper;
using ClothResorting.Dtos.Fba;
using ClothResorting.Models;
using ClothResorting.Models.FBAModels;
using ClothResorting.Models.FBAModels.StaticModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace ClothResorting.Controllers.Api.Fba
{
    public class FBAAllocatingController : ApiController
    {
        private ApplicationDbContext _context;

        public FBAAllocatingController()
        {
            _context = new ApplicationDbContext();
        }

        // GET /api/fba/fbaallocating/?grandNumber={grandNumber}
        [HttpGet]
        public IHttpActionResult GetAllocatablePallets([FromUri]string grandNumber)
        {
            return Ok(_context.FBAPallets.Where(x => x.GrandNumber == grandNumber
                && x.ActualPallets > x.ComsumedPallets).Select(Mapper.Map<FBAPallet, FBAPalletDto>));
        }

        // POST /api/fba/fbaallocating/?grandNumber={grandNumber}&inventoryType={inventoryType}
        [HttpPost]
        public void CreateLocationObjects([FromUri]string grandNumber, [FromUri]string inventoryType, [FromBody]IEnumerable<FBALocationDto> objArray)
        {
            if (inventoryType == FBAInventoryType.Pallet)
            {
                var palletLocationList = new List<FBAPalletLocation>();
                var palletsInDb = _context.FBAPallets.Where(x => x.GrandNumber == grandNumber
                    && x.ActualPallets - x.ComsumedPallets > 0);

                if (palletsInDb.Count() == 0)
                {
                    throw new Exception("No quantity for allocating.");
                }

                foreach (var obj in objArray)
                {
                    var palletInDb = palletsInDb.SingleOrDefault(x => x.Id == obj.Id);
                    palletInDb.ComsumedPallets += obj.Quantity;

                    if (palletInDb.ComsumedPallets > palletInDb.ActualPallets)
                    {
                        throw new Exception("Not enough quantity for comsuming. Check Id:" + obj.Id);
                    }

                    var palletLocation = new FBAPalletLocation();

                    palletLocation.Status = FBAStatus.InStock;
                    palletLocation.HowToDeliver = palletInDb.HowToDeliver;
                    palletLocation.GrossWeightPerPlt = palletInDb.ActualGrossWeight / palletInDb.ActualPallets;
                    palletLocation.CBMPerPlt = palletInDb.ActualCBM / palletInDb.ActualPallets;
                    palletLocation.CtnsPerPlt = palletInDb.ActualQuantity / palletInDb.ActualPallets;
                    palletLocation.AvailablePlts = obj.Quantity;
                    palletLocation.Location = obj.Location;
                    palletLocation.PalletSize = palletInDb.PalletSize;

                    palletLocation.AssembleFirstStringPart(palletInDb.ShipmentId, palletInDb.AmzRefId, palletInDb.WarehouseCode);
                    palletLocation.AssembleActualDetails(palletLocation.GrossWeightPerPlt * obj.Quantity, palletLocation.CBMPerPlt * obj.Quantity, obj.Quantity);
                    palletLocation.AssembleUniqueIndex(palletInDb.Container, palletInDb.GrandNumber);

                    palletLocation.FBAPallet = palletInDb;

                    palletLocationList.Add(palletLocation);
                }
                _context.FBAPalletLocations.AddRange(palletLocationList);
            }
            else
            {
                var cartonLocationList = new List<FBACartonLocation>();
                var orderDetailsInDb = _context.FBAOrderDetails
                    .Where(x => x.GrandNumber == grandNumber
                        && x.ActualQuantity - x.ComsumedQuantity > 0);

                if (orderDetailsInDb.Count() == 0)
                {
                    throw new Exception("No quantity for allocating.");
                }

                foreach (var obj in objArray)
                {
                    var orderDetailInDb = orderDetailsInDb.SingleOrDefault(x => x.Id == obj.Id);
                    orderDetailInDb.ComsumedQuantity += obj.Quantity;

                    if (orderDetailInDb.ComsumedQuantity > orderDetailInDb.ActualQuantity)
                    {
                        throw new Exception("Not enough quantity for comsuming. Check Id:" + obj.Id);
                    }

                    var cartonLocation = new FBACartonLocation();

                    cartonLocation.Status = FBAStatus.InStock;
                    cartonLocation.HowToDeliver = orderDetailInDb.HowToDeliver;
                    cartonLocation.GrossWeightPerCtn = (float)Math.Round((orderDetailInDb.ActualGrossWeight / orderDetailInDb.ActualQuantity), 2);
                    cartonLocation.CBMPerCtn = (float)Math.Round((orderDetailInDb.ActualCBM / orderDetailInDb.ActualQuantity), 2);
                    cartonLocation.AvailableCtns = obj.Quantity;
                    cartonLocation.Location = obj.Location;

                    cartonLocation.AssembleFirstStringPart(orderDetailInDb.ShipmentId, orderDetailInDb.AmzRefId, orderDetailInDb.WarehouseCode);
                    cartonLocation.AssembleActualDetails(cartonLocation.GrossWeightPerCtn * obj.Quantity, cartonLocation.CBMPerCtn * obj.Quantity, obj.Quantity);
                    cartonLocation.AssembleUniqueIndex(orderDetailInDb.Container, orderDetailInDb.GrandNumber);

                    cartonLocation.FBAOrderDetail = orderDetailInDb;

                    cartonLocationList.Add(cartonLocation);
                }

                _context.FBACartonLocations.AddRange(cartonLocationList);
            }
            _context.SaveChanges();
        }

    }

    public class FBALocationDto
    {
        public int Id { get; set; }

        public int Quantity { get; set; }

        public string Location { get; set; }
    }
}