namespace Hrms.Domain.Entities.EmployeeEntities
{

    //[DisableSoftDelete]
    public class AssignAsset : BaseEntity
    {
        public Guid? EmployeeId { get; set; }
        public string AssetType { get; set; }
        public string AssetDescription { get; set; }
        public string Model { get; set; }
        public string Brand { get; set; }
        public string SerialNo { get; set; }
        public int Qty { get; set; }
        public DateTime IssuanceDate { get; set; }
        public DateTime ReturnedDate { get; set; }
        public string Remarks { get; set; }
        public string File { get; set; }
    }
}
