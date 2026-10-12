using BuzlinkRepository;

namespace Hrms.Domain.Entities.Attachments;

/// <summary>What a file is attached to. Stored by name, so reordering never changes saved rows.</summary>
public enum AttachmentOwnerType
{
    EmployeeRecord,
    AssignAsset,
    LeaveApplication,
    ProfileUpdateRequest,
    TravelOrder,
    PassSlip,
    UnderTime,
    OverTime,
    Loan,
    CashBond,
    OtherIncome,
}

// One uploaded file (image or PDF) attached to a record. The file itself lives in file storage
// (StorageKey); this row is its owner, name and size. OwnerId is null while the file is only
// "staged" -- uploaded by a form whose record isn't saved yet -- and is set once the record exists.
// Hard-deleted on purpose: a removed attachment must not keep counting towards anything.
[DisableSoftDelete]
public class Attachment : BaseEntity
{
    public AttachmentOwnerType OwnerType { get; set; }
    public Guid? OwnerId { get; set; }

    /// <summary>The IFileStorage key; always under the company's own folder.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid? UploadedBy { get; set; }
}

// Whether a company lets a feature carry attachments. No row = off, so no company spends storage
// until an admin opts a feature in.
[DisableSoftDelete]
public class AttachmentSetting : BaseEntity
{
    public AttachmentOwnerType OwnerType { get; set; }
    public bool Enabled { get; set; }
}
