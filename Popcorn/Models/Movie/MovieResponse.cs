﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp.Deserializers;

namespace Popcorn.Models.Movie
{
    public class MovieResponse
    {
        [DeserializeAs(Name = "totalMovies")]
        public int TotalMovies { get; set; }

        [DeserializeAs(Name = "movies")]
        public List<MovieJson> Movies { get; set; }
    }
}
