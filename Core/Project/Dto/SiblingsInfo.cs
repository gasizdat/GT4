using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GT4.Core.Project.Dto;
internal record class SiblingsInfo(PersonFullInfo person, Dictionary<int, RelativeInfo[]> relatives) : Siblings;
