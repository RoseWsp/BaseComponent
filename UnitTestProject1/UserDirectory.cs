//------------------------------------------------------------------------------
// <auto-generated>
//     此代码已从模板生成。
//
//     手动更改此文件可能导致应用程序出现意外的行为。
//     如果重新生成代码，将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace UnitTestProject1
{
    using System;
    using System.Collections.Generic;
    
    public partial class UserDirectory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public int SyncDirectoryId { get; set; }
        public string ExcludePaths { get; set; }
        public int UserId { get; set; }
        public Nullable<System.DateTime> LastUpdateTime { get; set; }
    
        public virtual SyncDirectory SyncDirectory { get; set; }
        public virtual User User { get; set; }
    }
}
