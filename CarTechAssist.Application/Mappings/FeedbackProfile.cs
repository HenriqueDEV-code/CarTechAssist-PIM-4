using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoMapper;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Contracts.Enums;
using System.Threading.Tasks;

namespace CarTechAssist.Application.Mappings
{
    public class FeedbackProfile : Profile
    {
        public FeedbackProfile()
        {
            CreateMap<IAFeedbackScore, IAFeedbackScoreDto>().ReverseMap();
        }
    }
}
