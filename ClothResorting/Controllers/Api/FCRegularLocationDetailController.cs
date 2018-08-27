﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.Entity;
using ClothResorting.Models;
using AutoMapper;
using ClothResorting.Dtos;

namespace ClothResorting.Controllers.Api
{
    public class FCRegularLocationDetailController : ApiController
    {
        private ApplicationDbContext _context;

        public FCRegularLocationDetailController()
        {
            _context = new ApplicationDbContext();
        }

        // GET /api/fcregularlocationdetail/{preid}
        public IHttpActionResult GetAllLocationDetail([FromUri]int id)
        {
            var resultDto = _context.FCRegularLocationDetails
                .Include(c => c.PreReceiveOrder)
                .Where(c => c.PreReceiveOrder.Id == id)
                .ToList()
                .Select(Mapper.Map<FCRegularLocationDetail, FCRegularLocationDetailDto>);

            return Ok(resultDto);
        }

        // DELETE /api/FCRegularLocationDetail/{id} 删除库存记录，将记录的箱数件数移回SKU待分配
        [HttpDelete]
        public void RelocateLocation([FromUri]int id)
        {
            var locationInDb = _context.FCRegularLocationDetails
                .Include(x => x.PreReceiveOrder)
                .Include(x => x.RegularCaronDetail)
                .SingleOrDefault(x => x.Id == id);

            var preId = locationInDb.PreReceiveOrder.Id;

            //首先将宿主箱返回到待分配状态
            locationInDb.RegularCaronDetail.ToBeAllocatedCtns += locationInDb.AvailableCtns;
            locationInDb.RegularCaronDetail.ToBeAllocatedPcs += locationInDb.AvailablePcs;
            locationInDb.RegularCaronDetail.Status = "Reallocating";

            locationInDb.AvailablePcs = 0;
            locationInDb.AvailableCtns = 0;
            locationInDb.Status = "Reallocated";

            if (locationInDb.ShippedPcs == 0)
            {
                _context.FCRegularLocationDetails.Remove(locationInDb);
            }

            //然后找到所有寄生箱对象，即找到在同一箱的其他size库存(range相同且可用箱数为0的对象)
            var locationsInDb = _context.FCRegularLocationDetails
                .Include(x => x.RegularCaronDetail.POSummary.PreReceiveOrder)
                .Where(x => x.RegularCaronDetail.POSummary.PreReceiveOrder.Id == preId
                    && x.PurchaseOrder == locationInDb.PurchaseOrder
                    && x.Style == locationInDb.Style
                    && x.Color == locationInDb.Color
                    && x.CartonRange == locationInDb.CartonRange
                    && x.AvailableCtns == 0);

            foreach(var location in locationsInDb)
            {
                var cartonDetailInDb = location.RegularCaronDetail;

                var availableCtns = location.AvailableCtns;
                var availablePcs = location.AvailablePcs;
                //var pickingCtns = location.PickingCtns;
                //var pickingPcs = location.PickingPcs;
                //var shippedCtns = location.ShippedCtns;
                var shippedPcs = location.ShippedPcs;

                //当pickingCtns不为0时，说明有货正在拣，不能进行移库操作。此项限制在前端完成
                //当库存剩余为0且没有货在拣，也不能进行移库操作。此项限制在前端完成

                cartonDetailInDb.ToBeAllocatedCtns += availableCtns;
                cartonDetailInDb.ToBeAllocatedPcs += availablePcs;
                cartonDetailInDb.Status = "Realllocating";

                location.AvailableCtns = 0;
                location.AvailablePcs = 0;
                location.Status = "Reallocated";

                //当正在拣货数量不为零时，不能移库（在前端实现）
                //当有库存没有已发出去的货时，删除库存记录(否则不删除记录)，将库存记录的剩余库存移至SKU待分配页面
                if (shippedPcs == 0)
                {
                    _context.FCRegularLocationDetails.Remove(location);
                }
            }

            _context.SaveChanges();
        }
    }
}
