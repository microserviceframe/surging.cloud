﻿using Microsoft.Extensions.Configuration;
using Surging.Core.Quartz.Configurations;
using System;
using System.Collections.Generic;

namespace Surging.Core.Quartz
{
    public class AppConfig
    {
        internal static string Path;
        internal static IConfigurationRoot Configuration { get; set; }

        public static IConfigurationSection GetSection(string name)
        {
            return Configuration?.GetSection(name);
        }

        public static ICollection<JobOption> JobOptions { get; internal set; }

        public static bool IsClustered { get; internal set; }

        public static bool ImmediateStart { get; internal set; }

        public static ClusterOption ClusterOption { get; internal set; }
    }
}
