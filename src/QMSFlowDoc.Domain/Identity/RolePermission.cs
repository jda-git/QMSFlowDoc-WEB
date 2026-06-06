using System;

namespace QMSFlowDoc.Domain.Identity
{
    public class RolePermission
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoleId { get; set; }
        public string Section { get; set; } = string.Empty; // "Documents", "Inventory", "Staff"
        
        public bool CanRead { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanPrint { get; set; }
        public bool CanApprove { get; set; }
    }
}
