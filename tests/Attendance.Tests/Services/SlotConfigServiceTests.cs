using Attendance.Application.DTOs;
using Attendance.Application.Interfaces;
using Attendance.Application.Services;
using FluentValidation;
using Moq;

namespace Attendance.Tests.Services;

public class SlotConfigServiceTests
{
    private readonly Mock<ISlotConfigRepository> _repositoryMock;
    private readonly SlotConfigService _service;

    public SlotConfigServiceTests()
    {
        _repositoryMock = new Mock<ISlotConfigRepository>();
        _service = new SlotConfigService(_repositoryMock.Object);
    }

    [Fact]
    public async Task CreateSlotAsync_AdjacentTimeWindow_StartEqualsExistingEnd_Succeeds()
    {
        // Existing slot: LunchOut (12:00 - 13:00)
        var existingSlots = new List<SlotConfigDto>
        {
            new(1, "LunchOut", new TimeOnly(12, 0), new TimeOnly(13, 0), 0, true, true)
        };
        _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(existingSlots);
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<CreateSlotRequest>()))
            .ReturnsAsync((CreateSlotRequest req) => new SlotConfigDto(2, req.SlotName, req.StartTime, req.EndTime, req.GracePeriodMinutes, req.IsMandatory, true));

        // Create new slot: LunchIn (13:00 - 15:00) - starts exactly when LunchOut ends
        var request = new CreateSlotRequest("LunchIn", new TimeOnly(13, 0), new TimeOnly(15, 0));

        var result = await _service.CreateSlotAsync(request);

        Assert.NotNull(result);
        Assert.Equal("LunchIn", result.SlotName);
        Assert.Equal(new TimeOnly(13, 0), result.StartTime);
        Assert.Equal(new TimeOnly(15, 0), result.EndTime);
    }

    [Fact]
    public async Task CreateSlotAsync_AdjacentTimeWindow_EndEqualsExistingStart_Succeeds()
    {
        // Existing slot: EveningOut (15:00 - 17:00)
        var existingSlots = new List<SlotConfigDto>
        {
            new(1, "EveningOut", new TimeOnly(15, 0), new TimeOnly(17, 0), 0, true, true)
        };
        _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(existingSlots);
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<CreateSlotRequest>()))
            .ReturnsAsync((CreateSlotRequest req) => new SlotConfigDto(2, req.SlotName, req.StartTime, req.EndTime, req.GracePeriodMinutes, req.IsMandatory, true));

        // Create new slot: LunchIn (13:00 - 15:00) - ends exactly when EveningOut starts
        var request = new CreateSlotRequest("LunchIn", new TimeOnly(13, 0), new TimeOnly(15, 0));

        var result = await _service.CreateSlotAsync(request);

        Assert.NotNull(result);
        Assert.Equal("LunchIn", result.SlotName);
    }

    [Fact]
    public async Task CreateSlotAsync_TrueIntersectingOverlap_ThrowsValidationException()
    {
        // Existing slot: LunchOut (12:00 - 13:00)
        var existingSlots = new List<SlotConfigDto>
        {
            new(1, "LunchOut", new TimeOnly(12, 0), new TimeOnly(13, 0), 0, true, true)
        };
        _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(existingSlots);

        // Try to create overlapping slot: 12:30 - 14:00
        var request = new CreateSlotRequest("LunchIn", new TimeOnly(12, 30), new TimeOnly(14, 0));

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateSlotAsync(request));
    }

    [Fact]
    public async Task UpdateSlotAsync_ExpandingTimeWindow_ExcludesSelfFromOverlapCheck_Succeeds()
    {
        // User changed LunchIn (slot ID 2) from 1:00 PM–2:00 PM to 1:00 PM–3:00 PM.
        // LunchOut (slot ID 1) ends at 1:00 PM (12:00 - 13:00).
        var existingSlots = new List<SlotConfigDto>
        {
            new(1, "LunchOut", new TimeOnly(12, 0), new TimeOnly(13, 0), 0, true, true),
            new(2, "LunchIn", new TimeOnly(13, 0), new TimeOnly(14, 0), 0, true, true)
        };
        _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(existingSlots);
        _repositoryMock.Setup(r => r.UpdateAsync(2, It.IsAny<UpdateSlotRequest>()))
            .ReturnsAsync((int id, UpdateSlotRequest req) => new SlotConfigDto(id, req.SlotName, req.StartTime, req.EndTime, req.GracePeriodMinutes, req.IsMandatory, true));

        // Update LunchIn (ID 2) to 13:00 - 15:00
        var updateRequest = new UpdateSlotRequest("LunchIn", new TimeOnly(13, 0), new TimeOnly(15, 0));

        var updated = await _service.UpdateSlotAsync(2, updateRequest);

        Assert.NotNull(updated);
        Assert.Equal(new TimeOnly(13, 0), updated.StartTime);
        Assert.Equal(new TimeOnly(15, 0), updated.EndTime);
    }

    [Fact]
    public async Task UpdateSlotAsync_OverlappingWithDifferentActiveSlot_ThrowsValidationException()
    {
        // Existing slots: LunchOut (12:00 - 13:00), EveningOut (15:00 - 17:00)
        var existingSlots = new List<SlotConfigDto>
        {
            new(1, "LunchOut", new TimeOnly(12, 0), new TimeOnly(13, 0), 0, true, true),
            new(2, "EveningOut", new TimeOnly(15, 0), new TimeOnly(17, 0), 0, true, true)
        };
        _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(existingSlots);

        // Update LunchOut (ID 1) to overlap EveningOut: 12:00 - 15:30
        var updateRequest = new UpdateSlotRequest("LunchOut", new TimeOnly(12, 0), new TimeOnly(15, 30));

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateSlotAsync(1, updateRequest));
    }

    [Fact]
    public async Task ValidateAsync_InactiveOverlappingSlot_IsIgnored()
    {
        // Existing slot is inactive: LunchOut (12:00 - 13:00, IsActive = false)
        var existingSlots = new List<SlotConfigDto>
        {
            new(1, "LunchOut", new TimeOnly(12, 0), new TimeOnly(13, 0), 0, true, false)
        };
        _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(existingSlots);
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<CreateSlotRequest>()))
            .ReturnsAsync((CreateSlotRequest req) => new SlotConfigDto(2, req.SlotName, req.StartTime, req.EndTime, req.GracePeriodMinutes, req.IsMandatory, true));

        // Create new slot overlapping inactive slot: 12:30 - 14:00
        var request = new CreateSlotRequest("LunchIn", new TimeOnly(12, 30), new TimeOnly(14, 0));

        var result = await _service.CreateSlotAsync(request);

        Assert.NotNull(result);
    }
}
