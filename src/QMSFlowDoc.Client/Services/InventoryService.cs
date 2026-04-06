using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IInventoryService
{
    Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null);
    Task<Reagent?> GetReagentByIdAsync(Guid id);
    Task<Reagent?> CreateReagentAsync(CreateReagentRequest request);
    Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request);
    Task<bool> UpdateReagentStatusAsync(Guid id, int status);
    Task<List<ReagentLot>?> RegisterLotAsync(RegisterLotRequest request);
    Task<bool> AdjustStockAsync(AdjustStockRequest request);
    Task<bool> DeleteReagentAsync(Guid id);
    Task<List<InventoryMovementDto>> GetMovementsAsync(DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId);
}

/// <summary>
/// V2: Inventory service using SQL Server via EF Core.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly ClientDbContextFactory _dbFactory;

    public InventoryService(ClientDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null)
    {
        using var ctx = _dbFactory.CreateContext();
        var query = ctx.Reagents
            .Include(r => r.Supplier)
            .Include(r => r.Lots)
            .AsQueryable();

        if (isActive.HasValue)
            query = isActive.Value
                ? query.Where(r => r.Status == ReagentStatus.ACTIVO)
                : query.Where(r => r.Status != ReagentStatus.ACTIVO);

        var list = await query.OrderBy(r => r.Name)
            .Select(r => new ReagentListDto
            {
                Id = r.Id,
                Name = r.Name,
                Manufacturer = r.Manufacturer,
                Reference = r.Reference,
                ReagentType = r.ReagentType,
                Classification = r.Classification,
                Status = r.Status,
                SupplierName = r.Supplier != null ? r.Supplier.Name : null,
                TotalStock = r.Lots.Where(l => l.Status != LotStatus.CONSUMED).Sum(l => l.AvailableQty),
                MinStock = r.MinStock,
                NearestExpiry = r.Lots
                    .Where(l => l.Status != LotStatus.CONSUMED)
                    .OrderBy(l => l.ExpiryDate)
                    .Select(l => (DateTime?)l.ExpiryDate)
                    .FirstOrDefault()
            })
            .ToListAsync();

        if (isLowStock == true)
            return list.Where(r => r.TotalStock < r.MinStock);

        return list;
    }

    public async Task<Reagent?> GetReagentByIdAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Reagents
            .Include(r => r.Supplier)
            .Include(r => r.DefaultLocation)
            .Include(r => r.Lots.OrderByDescending(l => l.CreatedAt))
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Reagent?> CreateReagentAsync(CreateReagentRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var reagent = new Reagent
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Manufacturer = request.Manufacturer,
            ReagentType = request.ReagentType,
            Reference = request.Reference,
            Classification = request.Classification,
            StorageConditions = request.StorageConditions,
            OpenShelfLifeDays = request.OpenShelfLifeDays,
            MinStock = request.MinStock,
            TargetStock = request.TargetStock,
            ReorderQty = request.ReorderQty,
            Status = ReagentStatus.ACTIVO,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Fluorescence = request.Fluorescence ?? "",
            ManufacturerCode = request.ManufacturerCode,
            InternalCode = request.InternalCode,
            SupplierId = request.SupplierId,
            DefaultLocationId = request.DefaultLocationId
        };
        ctx.Reagents.Add(reagent);
        await ctx.SaveChangesAsync();
        return reagent;
    }

    public async Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var r = await ctx.Reagents.FindAsync(id);
        if (r == null) return false;

        r.Name = request.Name;
        r.Manufacturer = request.Manufacturer;
        r.ReagentType = request.ReagentType;
        r.Reference = request.Reference;
        r.Classification = request.Classification;
        r.StorageConditions = request.StorageConditions;
        r.OpenShelfLifeDays = request.OpenShelfLifeDays;
        r.MinStock = request.MinStock;
        r.TargetStock = request.TargetStock;
        r.ReorderQty = request.ReorderQty;
        r.Fluorescence = request.Fluorescence ?? "";
        r.ManufacturerCode = request.ManufacturerCode;
        r.InternalCode = request.InternalCode;
        r.SupplierId = request.SupplierId;
        r.DefaultLocationId = request.DefaultLocationId;
        r.UpdatedAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateReagentStatusAsync(Guid id, int status)
    {
        using var ctx = _dbFactory.CreateContext();
        var r = await ctx.Reagents.FindAsync(id);
        if (r == null) return false;

        r.Status = (ReagentStatus)status;
        r.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<ReagentLot>?> RegisterLotAsync(RegisterLotRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var lot = new ReagentLot
        {
            Id = Guid.NewGuid(),
            ReagentId = request.ReagentId,
            LotNumber = request.LotNumber,
            ReceivedQty = request.ReceivedQty,
            AvailableQty = request.ReceivedQty,
            ExpiryDate = request.ExpiryDate,
            ReceivedDate = request.ReceivedDate,
            LocationId = request.LocationId,
            Status = LotStatus.RELEASED,
            CreatedAt = DateTime.UtcNow
        };
        ctx.ReagentLots.Add(lot);

        // Record inventory movement
        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ReagentId = request.ReagentId,
            ReagentLotId = lot.Id,
            Qty = request.ReceivedQty,
            MovementType = InventoryMovementType.IN,
            Reason = $"Lote {request.LotNumber} recibido",
            MovedAt = DateTime.UtcNow
        };
        ctx.InventoryMovements.Add(movement);

        await ctx.SaveChangesAsync();

        // Return updated lots
        return await ctx.ReagentLots
            .Where(l => l.ReagentId == request.ReagentId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> AdjustStockAsync(AdjustStockRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var lot = await ctx.ReagentLots.FindAsync(request.ReagentLotId);
        if (lot == null) return false;

        lot.AvailableQty += request.Qty;
        if (lot.AvailableQty <= 0)
        {
            lot.AvailableQty = 0;
            lot.Status = LotStatus.CONSUMED;
        }

        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ReagentId = lot.ReagentId,
            ReagentLotId = lot.Id,
            Qty = request.Qty,
            MovementType = request.MovementType,
            Reason = request.Reason,
            MovedAt = DateTime.UtcNow
        };
        ctx.InventoryMovements.Add(movement);

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteReagentAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        var r = await ctx.Reagents.FindAsync(id);
        if (r == null) return false;

        r.IsDeleted = true;
        r.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<InventoryMovementDto>> GetMovementsAsync(
        DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId)
    {
        using var ctx = _dbFactory.CreateContext();
        var query = ctx.InventoryMovements
            .Include(m => m.Reagent)
            .Include(m => m.ReagentLot)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(m => m.MovedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(m => m.MovedAt <= to.Value);
        if (type.HasValue)
            query = query.Where(m => m.MovementType == type.Value);
        if (reagentId.HasValue)
            query = query.Where(m => m.ReagentId == reagentId.Value);

        return await query
            .OrderByDescending(m => m.MovedAt)
            .Take(500)
            .Select(m => new InventoryMovementDto(
                m.Id,
                m.MovedAt,
                "Sistema", // Or join with Users to get real name
                m.Reagent != null ? m.Reagent.Name : "?",
                m.Reagent != null ? m.Reagent.Manufacturer : null,
                m.Reagent != null ? m.Reagent.Fluorescence : null,
                m.MovementType.ToString(),
                m.Qty,
                m.ReagentLot != null ? m.ReagentLot.LotNumber : null,
                m.ReagentLot != null ? m.ReagentLot.ExpiryDate : null,
                m.Reason ?? ""
            ))
            .ToListAsync();
    }
}
