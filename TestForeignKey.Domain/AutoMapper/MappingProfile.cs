﻿using AutoMapper;
using System;
using System.Collections.Generic;
using System.Text;
using TestForeignKey.Domain.Entities;
using TestForeignKey.Domain.ViewModels;

namespace TestForeignKey.Domain.AutoMapper
{
    public class MappingProfile : Profile
    {
        protected MappingProfile(string profileName) : base(profileName)
        {

        }

        public MappingProfile()
        {
            CreateMap<Many, ManyQueryViewModel>()
                .ForMember(p => p.CustomProperty, opts => opts.MapFrom(p => $"ManyID: { p.ManyID }/OneID: { p.OneID }/ManyProperty01: {p.ManyProperty01}/OneProperty01: {p.One.OneProperty01}"));
        }
    }
}
