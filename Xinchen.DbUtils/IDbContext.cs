﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xinchen.DbUtils
{
    public interface IDbContext
    {
        void SaveChanges();
    }
}