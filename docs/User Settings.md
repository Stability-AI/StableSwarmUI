# User Settings in StableSwarmUI

(TODO: general info about settings)

## Path Format

`User -> OutpathBuilder -> Format` accepts the following format keys:

- `[year]`: 4-digit year, eg 2023
- `[month]`: 2-digit month, eg 07
- `[month_name]`: full month name, eg July
- `[day]`: 2-digit day, eg 29
- `[day_name]`: full day name, eg Saturday
- `[hour]`: 2-digit hour, eg 12
- `[minute]`: 2-digit minute, eg 04
- `[second]`: 2-digit second, eg 30
- `[prompt]`: the prompt (often cut off by `MaxLenPerPart`)
- `[negative_prompt]`: the negative prompt (often cut off by `MaxLenPerPart`)
- `[seed]`: the seed number parameter
- `[cfg_scale]`: the CFG Scale parameter
- `[width]`: the Width parameter
- `[height]`: the Height parameter
- `[steps]`: the Steps number parameter
- `[model]`: the filename of the model
- `[model_title]`: the metadata title of the model
- `[user_name]`: the name of the user
- `[batch_id]`: the index # of this image within the batch
- `[some parameter name here]`: the value of the parameter named. Must have exact parameter name. For example `[refinermodel]` will get you the name of the refiner model.

If names overlap, a numeric index will be appended to the end, eg if `123-a cat.jpg` is your output but it already exists, `123-a cat-1.jpg` will be used.
