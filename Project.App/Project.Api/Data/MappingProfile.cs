using AutoMapper;
using Project.Api.DTOs;
using Project.Api.Models;

namespace Project.Api.Data;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Hand, HandDTO>().ReverseMap();
        CreateMap<Hand, HandUpdateDTO>().ReverseMap();
        CreateMap<Hand, HandPatchDTO>().ReverseMap();
    }
}
