﻿using AutoMapper;
using ClothResorting.Controllers.Api.Warehouse;
using ClothResorting.Dtos;
using ClothResorting.Dtos.Fba;
using ClothResorting.Models;
using ClothResorting.Models.FBAModels;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ClothResorting.App_Start
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            //DefaultConnection
            Mapper.CreateMap<PreReceiveOrder, PreReceiveOrdersDto>();
            Mapper.CreateMap<PurchaseOrderSummary, PurchaseOrderSummaryDto>();
            Mapper.CreateMap<Measurement, MeasurementDto>();
            Mapper.CreateMap<CartonDetail, CartonDetailDto>();
            Mapper.CreateMap<SizeRatio, SizeRatioDto>();
            Mapper.CreateMap<CartonBreakDown, CartonBreakDownDto>();
            Mapper.CreateMap<ReplenishmentLocationDetail, ReplenishmentLocationDetailDto>();
            //Mapper.CreateMap<PermanentLocIORecord, PermanentLocIORecordDto>();
            //Mapper.CreateMap<PermanentLocation, PermanentLocationDto>();
            Mapper.CreateMap<PurchaseOrderInventory, PurchaseOrderInventoryDto>();
            Mapper.CreateMap<SpeciesInventory, SpeciesInventoryDto>();
            Mapper.CreateMap<AdjustmentRecord, AdjustmentRecordDto>();
            Mapper.CreateMap<POSummary, POSummaryDto>();
            Mapper.CreateMap<RegularCartonDetail, RegularCartonDetailDto>();
            Mapper.CreateMap<FCRegularLocationDetail, FCRegularLocationDetailDto>();
            Mapper.CreateMap<ShipOrder, ShipOrderDto>();
            Mapper.CreateMap<ShipOrder, ShipOrderDto>();
            Mapper.CreateMap<PickDetail, PickDetailDto>();
            Mapper.CreateMap<PullSheetDiagnostic, PullSheetDiagnosticDto>();
            Mapper.CreateMap<Container, ContainerDto>();
            Mapper.CreateMap<GeneralLocationSummary, GeneralLocationSummaryDto>();
            Mapper.CreateMap<UpperVendor, UpperVendorDto>();
            Mapper.CreateMap<ChargingItem, ChargingItemDto>();
            Mapper.CreateMap<Invoice, InvoiceDto>();
            Mapper.CreateMap<InvoiceDetail, InvoiceDetailDto>();
            Mapper.CreateMap<PermanentSKU, PermanentSKUDto>();
            Mapper.CreateMap<OutboundHistory, OutboundHistoryDto>();
            Mapper.CreateMap<NameCrossReference, NameCrossReferenceDto>();

            //FBA(Under DefaultConnection)
            Mapper.CreateMap<FBAMasterOrder, FBAMasterOrderDto>();
            Mapper.CreateMap<FBAOrderDetail, FBAOrderDetailDto>()
                .ForMember(dest => dest.LabelFileNumbers, opt => opt.MapFrom(src => src.LabelFiles.Split('{').Length - 1));
            Mapper.CreateMap<FBACartonLocation, FBACartonLocationDto>()
                .ForMember(dest => dest.Barcode, opt => opt.MapFrom(src => src.FBAOrderDetail.Barcode))
                .ForMember(dest => dest.LabelFileNumbers, opt => opt.MapFrom(src => (src.FBAOrderDetail == null ? -1 : src.FBAOrderDetail.LabelFiles.Split('{').Length - 1)));
            Mapper.CreateMap<FBAPallet, FBAPalletDto>();
            Mapper.CreateMap<FBAPalletLocation, FBAPalletLocationDto>();
            Mapper.CreateMap<FBAShipOrder, FBAShipOrderDto>();
            Mapper.CreateMap<FBAPickDetail, FBAPickDetailsDto>();
                //.ForMember(dest => dest.Barcode, opt => opt.MapFrom(src => "MIX"));
            Mapper.CreateMap<FBAAddressBook, FBAAddressBookDto>();
            Mapper.CreateMap<ChargingItemDetail, ChargingItemDetailDto>();
            Mapper.CreateMap<FBAShipOrder, WarehouseOutboundLog>();

            //FBAConnection
            Mapper.CreateMap<ChargeTemplate, ChargeTemplateDto>();
            Mapper.CreateMap<ChargeMethod, ChargeMethodDto>();

            //General
            Mapper.CreateMap<EFile, EFileDto>();
            Mapper.CreateMap<ApplicationUser, ApplicationUserDto>();
            Mapper.CreateMap<IdentityRole, IdentityRoleDto>();
            Mapper.CreateMap<InstructionTemplate, InstructionTemplateDto>();
        }
    }
}